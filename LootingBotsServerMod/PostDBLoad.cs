using System.Reflection;
using LootingBotsServerMod.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace LootingBotsServerMod;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class PostDBLoad(
    TemplateTable templateTable,
    GlobalTable globalTable,
    BotTable botTable,
    PmcConfig pmcConfig,
    BotConfig botConfig,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    ISptLogger<PostDBLoad> logger
) : IOnLoad
{
    private readonly ConfigModel _config =
        jsonUtil.DeserializeFromFile<ConfigModel>(
            Path.Join(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config", "config.json")
        ) ?? new();
    private readonly PmcConfig _pmcConfig = pmcConfig;
    private readonly BotConfig _botConfig = botConfig;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (!_config.PmcSpawnWithLoot)
        {
            EmptyInventory(["usec", "pmcusec", "bear", "pmcbear"]);

            // Do not allow weapons to spawn in PMC bags
            _pmcConfig.LooseWeaponInBackpackLootMinMax.Max = 0;

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

        logger.Info("[LootingBots-ServerMod] Marking items with DiscardLimits as InsuranceDisabled");

        foreach ((_, var template) in templateTable.Items)
        {
            /**
           * When we set DiscardLimitsEnabled to false further down, this will cause some items to be able to be insured when they normally should not be.
           * The DiscardLimit property is used by BSG for RMT protections and their code internally treats things with discard limits as not insurable.
           * For items that have a DiscardLimit >= 0, we need to manually flag them as InsuranceDisabled to make sure they still cannot be insured by the player.
           * Do not disable insurance if the item is marked as always available for insurance.
           */

            if (template.Properties is null)
            {
                continue;
            }

            if (template.Properties.DiscardLimit >= 0 && template.Properties.IsAlwaysAvailableForInsurance == false)
            {
                template.Properties.InsuranceDisabled = true;
            }
        }

        globalTable.Configuration.DiscardLimitsEnabled = false;
        logger.Info("[LootingBots-ServerMod] Global config DiscardLimitsEnabled set to false");

        return Task.CompletedTask;
    }

    protected void EmptyInventory(List<string> botTypes)
    {
        foreach (var botType in botTypes)
        {
            logger.Info($"[LootingBots-ServerMod] Removing loot from {botType}");
            var backpackWeights = botTable.Types[botType].BotGeneration.Items.BackpackLoot.Weights;
            var vestWeights = botTable.Types[botType].BotGeneration.Items.VestLoot.Weights;
            var pocketLootWeights = botTable.Types[botType].BotGeneration.Items.PocketLoot.Weights;

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
