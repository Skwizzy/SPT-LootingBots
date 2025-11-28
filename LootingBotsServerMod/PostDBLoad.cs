using System.Reflection;
using LootingBotsServerMod.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace LootingBotsServerMod;

/// <summary>
/// Main server-side mod logic that runs after the database is loaded
/// Configures bot spawn loot and disables discard limits
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class PostDBLoad(
    DatabaseService databaseService,
    ConfigService configService,
    ModHelper modHelper,
    ISptLogger<PostDBLoad> logger
) : IOnLoad
{
    private ConfigModel? _config;

    public Task OnLoad()
    {
        // Load the mod's config file
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        _config = modHelper.GetJsonDataFromFile<ConfigModel>(pathToMod, "Config/config.json");

        var pmcConfig = configService.GetConfig(ConfigTypes.PMC);
        var botConfig = configService.GetConfig(ConfigTypes.BOT);

        if (!_config.PmcSpawnWithLoot)
        {
            EmptyInventory(["usec", "bear"]);

            // Do not allow weapons to spawn in PMC bags
            pmcConfig.LooseWeaponInBackpackLootMinMax.Max = 0;

            // Clear weights in pmc randomisation
            if (botConfig.Equipment?.Pmc?.Randomisation != null)
            {
                foreach (var details in botConfig.Equipment.Pmc.Randomisation)
                {
                    var generation = details?.Generation;
                    if (generation != null)
                    {
                        ClearWeights(generation.BackpackLoot?.Weights);
                        ClearWeights(generation.PocketLoot?.Weights);
                        ClearWeights(generation.VestLoot?.Weights);
                    }
                }
            }
        }

        if (!_config.ScavSpawnWithLoot)
        {
            EmptyInventory(["assault"]);
        }

        logger.Info("Marking items with DiscardLimits as InsuranceDisabled");

        var tables = databaseService.GetTables();
        foreach (var (itemId, template) in tables.Templates.Items)
        {
            /**
             * When we set DiscardLimitsEnabled to false further down, this will cause some items to be able to be insured when they normally should not be.
             * The DiscardLimit property is used by BSG for RMT protections and their code internally treats things with discard limits as not insurable.
             * For items that have a DiscardLimit >= 0, we need to manually flag them as InsuranceDisabled to make sure they still cannot be insured by the player.
             * Do not disable insurance if the item is marked as always available for insurance.
             */
            if (
                template.Properties.DiscardLimit >= 0
                && !template.Properties.IsAlwaysAvailableForInsurance
            )
            {
                template.Properties.InsuranceDisabled = true;
            }
        }

        tables.Globals.Configuration.DiscardLimitsEnabled = false;
        logger.Success("Global config DiscardLimitsEnabled set to false");

        return Task.CompletedTask;
    }

    private void EmptyInventory(List<string> botTypes)
    {
        var tables = databaseService.GetTables();
        foreach (var botType in botTypes)
        {
            logger.Info($"Removing loot from {botType}");
            var botGeneration = tables.Bots.Types[botType].BotGeneration.Items;
            
            ClearWeights(botGeneration.BackpackLoot?.Weights);
            ClearWeights(botGeneration.VestLoot?.Weights);
            ClearWeights(botGeneration.PocketLoot?.Weights);
        }
    }

    private void ClearWeights(Dictionary<double, double>? weights)
    {
        if (weights == null) return;
        
        foreach (var key in weights.Keys.ToList())
        {
            weights[key] = 0;
        }
    }
}
