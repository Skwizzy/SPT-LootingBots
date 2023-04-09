using BepInEx;
using BepInEx.Configuration;
using System;
using LootingBots.Patch;
using LootingBots.Patch.Util;
using Comfort.Common;
using EFT;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const String MOD_GUID = "me.skwizzy.lootingbots";
        private const String MOD_NAME = "LootingBots";
        private const String MOD_VERSION = "1.0.1";

        // Container Looting
        public static ConfigEntry<bool> containerLootingEnabled;
        public static ConfigEntry<BotType> dynamicContainerLootingEnabled;
        public static ConfigEntry<float> timeToWaitBetweenContainers;
        public static ConfigEntry<float> detectContainerDistance;
        public static ConfigEntry<bool> debugContainerNav;
        public static ConfigEntry<LogUtils.LogLevel> containerLogLevels;

        public static Log containerLog;

        // Corpse Looting
        public static ConfigEntry<LogUtils.LogLevel> corpseLogLevels;
        public static ConfigEntry<float> bodySeeDist;
        public static ConfigEntry<float> bodyLeaveDist;
        public static ConfigEntry<float> bodyLookPeriod;
        public static ConfigEntry<bool> useMarketPrices;
        public static ConfigEntry<bool> valueFromMods;
        public static ConfigEntry<BotType> lootingEnabledBots;
        public static Log lootLog;
        public static ItemAppraiser itemAppraiser = new ItemAppraiser();

        public void ContainerLootSettings()
        {
            containerLootingEnabled = Config.Bind(
                "Container Looting",
                "Enable reserve patrols",
                true,
                new ConfigDescription(
                    "Enable looting of containers for bots on patrols that stop in front of lootable containers",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            dynamicContainerLootingEnabled = Config.Bind(
                "Container Looting",
                "Enable dynamic looting",
                BotType.All,
                new ConfigDescription(
                    "Enable dynamic looting of containers, will detect containers within the set distance and navigate to them similar to how they would loot a corpse. More resource demanding than reserve patrol looting",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            timeToWaitBetweenContainers = Config.Bind(
                "Container Looting",
                "Dynamic looting: Delay between containers",
                45f,
                new ConfigDescription(
                    "The amount of time the bot will wait after looting a container before trying to find the next nearest contianer",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            detectContainerDistance = Config.Bind(
                "Container Looting",
                "Dynamic looting: Detect container distance",
                25f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            containerLogLevels = Config.Bind<LogUtils.LogLevel>(
                "Container Looting",
                "Log Levels",
                LogUtils.LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            debugContainerNav = Config.Bind(
                "Container Looting",
                "Debug: Show navigation points",
                false,
                new ConfigDescription(
                    "Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Black): 'Optimized' container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
                )
            );
        }

        public void CorpseLootSettings()
        {
            lootingEnabledBots = Config.Bind(
                "Corpse Looting",
                "Enable looting",
                BotType.All,
                new ConfigDescription(
                    "Enables corpse looting for the selected bot types. Takes affect during the generation of the next raid.",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );
            bodySeeDist = Config.Bind(
                "Corpse Looting",
                "Distance to see body",
                25f,
                new ConfigDescription(
                    "If the bot is with X meters, it can see the body",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            bodyLeaveDist = Config.Bind(
                "Corpse Looting",
                "Distance to forget body",
                50f,
                new ConfigDescription(
                    "If the bot is further than X meters, it will forget about the body",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            bodyLookPeriod = Config.Bind(
                "Corpse Looting",
                "Looting time (*)",
                8.0f,
                new ConfigDescription(
                    "Time bot stands at corpse looting. *WARNING: Shorter times may display strange behavior",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            corpseLogLevels = Config.Bind<LogUtils.LogLevel>(
                "Corpse Looting",
                "Log Levels",
                LogUtils.LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
        }

        public void WeaponLootSettings()
        {
            useMarketPrices = Config.Bind(
                "Weapon Looting",
                "Use flea market prices",
                false,
                new ConfigDescription(
                    "Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            valueFromMods = Config.Bind(
                "Weapon Looting",
                "Calculate value from attachments",
                true,
                new ConfigDescription(
                    "Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check. Disable if experiencing performance issues",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
                )
            );
        }

        public void Awake()
        {
            ContainerLootSettings();
            CorpseLootSettings();
            WeaponLootSettings();

            lootLog = new Log(Logger, corpseLogLevels);
            containerLog = new Log(Logger, containerLogLevels);

            new ContainerLooting().Enable();
            new CorpseLootSettingsPatch().Enable();
            new CorpseLootingPatch().Enable();
        }

        public void Update()
        {
            bool shoultInitAppraiser =
                (!useMarketPrices.Value && itemAppraiser.handbookData == null)
                || (useMarketPrices.Value && !itemAppraiser.marketInitialized);

            // Initialize the itemAppraiser when the BE instance comes online
            if (
                Singleton<ClientApplication<ISession>>.Instance != null
                && Singleton<GClass2529>.Instance != null
                && shoultInitAppraiser
            )
            {
                lootLog.logWarning($"Initializing item appraiser");
                itemAppraiser.init();
            }
        }
    }
}
