using BepInEx;
using BepInEx.Configuration;

using Comfort.Common;

using EFT;

using LootingBots.Patch;
using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const string MOD_GUID = "me.skwizzy.lootingbots";
        private const string MOD_NAME = "LootingBots";
        private const string MOD_VERSION = "1.0.2";

        // Container Looting
        public static ConfigEntry<bool> ContainerLootingEnabled;
        public static ConfigEntry<BotType> DynamicContainerLootingEnabled;
        public static ConfigEntry<float> TimeToWaitBetweenContainers;
        public static ConfigEntry<float> DetectContainerDistance;
        public static ConfigEntry<bool> DebugContainerNav;
        public static ConfigEntry<LogUtils.LogLevel> ContainerLogLevels;

        public static Log ContainerLog;

        // Corpse Looting
        public static ConfigEntry<LogUtils.LogLevel> CorpseLogLevels;
        public static ConfigEntry<float> BodySeeDist;
        public static ConfigEntry<float> BodyLeaveDist;
        public static ConfigEntry<float> BodyLookPeriod;
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<BotType> LootingEnabledBots;
        public static Log LootLog;
        public static ItemAppraiser ItemAppraiser = new ItemAppraiser();

        public void ContainerLootSettings()
        {
            ContainerLootingEnabled = Config.Bind(
                "Container Looting",
                "Enable reserve patrols",
                false,
                new ConfigDescription(
                    "Enable looting of containers for bots on patrols that stop in front of lootable containers",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            DynamicContainerLootingEnabled = Config.Bind(
                "Container Looting",
                "Enable dynamic looting",
                BotType.All,
                new ConfigDescription(
                    "Enable dynamic looting of containers, will detect containers within the set distance and navigate to them similar to how they would loot a corpse. More resource demanding than reserve patrol looting",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            TimeToWaitBetweenContainers = Config.Bind(
                "Container Looting",
                "Dynamic looting: Delay between containers",
                45f,
                new ConfigDescription(
                    "The amount of time the bot will wait after looting a container before trying to find the next nearest contianer",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            DetectContainerDistance = Config.Bind(
                "Container Looting",
                "Dynamic looting: Detect container distance",
                25f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            ContainerLogLevels = Config.Bind<LogUtils.LogLevel>(
                "Container Looting",
                "Log Levels",
                LogUtils.LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            DebugContainerNav = Config.Bind(
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
            LootingEnabledBots = Config.Bind(
                "Corpse Looting",
                "Enable looting",
                BotType.All,
                new ConfigDescription(
                    "Enables corpse looting for the selected bot types. Takes affect during the generation of the next raid.",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );
            BodySeeDist = Config.Bind(
                "Corpse Looting",
                "Distance to see body",
                25f,
                new ConfigDescription(
                    "If the bot is with X meters, it can see the body",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            BodyLeaveDist = Config.Bind(
                "Corpse Looting",
                "Distance to forget body",
                50f,
                new ConfigDescription(
                    "If the bot is further than X meters, it will forget about the body",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            BodyLookPeriod = Config.Bind(
                "Corpse Looting",
                "Looting time (*)",
                8.0f,
                new ConfigDescription(
                    "Time bot stands at corpse looting. *WARNING: Shorter times may display strange behavior",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            CorpseLogLevels = Config.Bind<LogUtils.LogLevel>(
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
            UseMarketPrices = Config.Bind(
                "Weapon Looting",
                "Use flea market prices",
                false,
                new ConfigDescription(
                    "Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            ValueFromMods = Config.Bind(
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

            LootLog = new Log(Logger, CorpseLogLevels);
            ContainerLog = new Log(Logger, ContainerLogLevels);

            new LootSettingsPatch().Enable();
            new ContainerLooting().Enable();
            new CorpseLootingPatch().Enable();
            new LooseLootPatch().Enable();
        }

        public void Update()
        {
            bool shoultInitAppraiser =
                (!UseMarketPrices.Value && ItemAppraiser.HandbookData == null)
                || (UseMarketPrices.Value && !ItemAppraiser.MarketInitialized);

            // Initialize the itemAppraiser when the BE instance comes online
            if (
                Singleton<ClientApplication<ISession>>.Instance != null
                && Singleton<GClass2532>.Instance != null
                && shoultInitAppraiser
            )
            {
                LootLog.LogWarning($"Initializing item appraiser");
                ItemAppraiser.Init();
            }
        }
    }
}