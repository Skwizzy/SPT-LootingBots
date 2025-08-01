using System.Reflection;
using LootingBotsServerMod.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace LootingBotsServerMod
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
    public class PostDBLoad(
        DatabaseServer databaseServer,
        ConfigServer configServer,
        JsonUtil jsonUtil,
        ModHelper modHelper,
        ISptLogger<PostDBLoad> logger
    ) : IOnLoad
    {
        private readonly ConfigModel _config =
            jsonUtil.DeserializeFromFile<ConfigModel>(
                System.IO.Path.Join(
                    modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()),
                    "config",
                    "config.json"
                )
            ) ?? new();
        private readonly PmcConfig _PmcConfig = configServer.GetConfig<PmcConfig>();
        private readonly BotConfig _BotConfig = configServer.GetConfig<BotConfig>();

        public Task OnLoad()
        {
            if (!_config.PmcSpawnWithLoot)
            {
                EmptyInventory(["usec", "bear"]);

                // Do not allow weapons to spawn in PMC bags
                _PmcConfig.LooseWeaponInBackpackLootMinMax.Max = 0;

                /* Is this even necessary? I have no idea for how long this was broken in Node
                foreach((string bot, EquipmentFilters? filter) in _BotConfig.Equipment)
                {
                    foreach (var randomisationDetail in filter.Randomisation)
                    {
                    }
                }
                */
            }

            if (!_config.ScavSpawnWithLoot)
            {
                EmptyInventory(["assault"]);
            }

            logger.Info(
                "[LootingBots-ServerMod] Marking items with DiscardLimits as InsuranceDisabled"
            );

            foreach (
                (string itemId, TemplateItem template) in databaseServer.GetTables().Templates.Items
            )
            {
                /**
               * When we set DiscardLimitsEnabled to false further down, this will cause some items to be able to be insured when they normally should not be.
               * The DiscardLimit property is used by BSG for RMT protections and their code internally treats things with discard limits as not insurable.
               * For items that have a DiscardLimit >= 0, we need to manually flag them as InsuranceDisabled to make sure they still cannot be insured by the player.
               * Do not disable insurance if the item is marked as always available for insurance.
               */
                if (
                    template.Properties.DiscardLimit >= 0
                    && template.Properties.IsAlwaysAvailableForInsurance == false
                )
                {
                    template.Properties.InsuranceDisabled = true;
                }
            }

            databaseServer.GetTables().Globals.Configuration.DiscardLimitsEnabled = false;
            logger.Info("[LootingBots-ServerMod] Global config DiscardLimitsEnabled set to false");

            return Task.CompletedTask;
        }

        protected void EmptyInventory(List<string> botTypes)
        {
            foreach (var botType in botTypes)
            {
                logger.Info($"[LootingBots-ServerMod] Removing loot from {botType}");
                var backpackWeights = databaseServer
                    .GetTables()
                    .Bots.Types[botType]
                    .BotGeneration.Items.BackpackLoot.Weights;
                var vestWeights = databaseServer
                    .GetTables()
                    .Bots.Types[botType]
                    .BotGeneration.Items.VestLoot.Weights;
                var pocketLootWeights = databaseServer
                    .GetTables()
                    .Bots.Types[botType]
                    .BotGeneration.Items.PocketLoot.Weights;

                ClearWeights(backpackWeights);
                ClearWeights(vestWeights);
                ClearWeights(pocketLootWeights);
            }
        }

        protected void ClearWeights(Dictionary<double, double> weights)
        {
            foreach (var key in weights.Keys.ToList())
            {
                weights[key] = 0;
            }
        }
    }
}
