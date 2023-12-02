using BepInEx;
using BepInEx.Configuration;

using Comfort.Common;

using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;
using LootingBots.Patch;
using LootingBots.Brain;

using DrakiaXYZ.BigBrain.Brains;
using System.Collections.Generic;

using HandbookClass = GClass2691;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInDependency("xyz.drakia.bigbrain", "0.1.4")]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const string MOD_GUID = "me.skwizzy.lootingbots";
        private const string MOD_NAME = "LootingBots";
        private const string MOD_VERSION = "1.1.4";

        public const BotType SettingsDefaults = BotType.Scav | BotType.Pmc | BotType.Raider;

        // Loot Finder Settings
        public static ConfigEntry<BotType> CorpseLootingEnabled;
        public static ConfigEntry<BotType> ContainerLootingEnabled;
        public static ConfigEntry<BotType> LooseItemLootingEnabled;
        public static ConfigEntry<float> InitialStartTimer;

        public static ConfigEntry<float> LootScanInterval;
        public static ConfigEntry<float> DetectItemDistance;
        public static ConfigEntry<float> DetectContainerDistance;
        public static ConfigEntry<float> DetectCorpseDistance;

        public static ConfigEntry<bool> DebugLootNavigation;
        public static ConfigEntry<LogLevel> LootingLogLevels;
        public static Log LootLog;

        // Loot Settings
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<int> TransactionDelay;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<bool> CanStripAttachments;

        public static ConfigEntry<float> PMCMinLootThreshold;
        public static ConfigEntry<float> PMCMaxLootThreshold;
        public static ConfigEntry<float> ScavMinLootThreshold;
        public static ConfigEntry<float> ScavMaxLootThreshold;

        public static ConfigEntry<EquipmentType> PMCGearToEquip;
        public static ConfigEntry<EquipmentType> PMCGearToPickup;
        public static ConfigEntry<EquipmentType> ScavGearToEquip;
        public static ConfigEntry<EquipmentType> ScavGearToPickup;

        public static ConfigEntry<LogLevel> ItemAppraiserLogLevels;
        public static Log ItemAppraiserLog;
        public static ItemAppraiser ItemAppraiser = new ItemAppraiser();

        public void LootFinderSettings()
        {
            CorpseLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable corpse looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables corpse looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );

            DetectCorpseDistance = Config.Bind(
                "Loot Finder",
                "Detect corpse distance",
                75f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a corpse",
                    null,
                    new ConfigurationManagerAttributes { Order = 8 }
                )
            );
            ContainerLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable container looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables container looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 7 }
                )
            );
            DetectContainerDistance = Config.Bind(
                "Loot Finder",
                "Detect container distance",
                75f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            LooseItemLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable loose item looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables loose item looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            DetectItemDistance = Config.Bind(
                "Loot Finder",
                "Detect item distance",
                75f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect an item",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            LootingLogLevels = Config.Bind(
                "Loot Finder",
                "Log Levels",
                LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
                )
            );
            DebugLootNavigation = Config.Bind(
                "Loot Finder",
                "Debug: Show navigation points",
                false,
                new ConfigDescription(
                    "Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Black): 'Optimized' container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).",
                    null,
                    new ConfigurationManagerAttributes { Order = -1 }
                )
            );

            // Loot Finder (Timing)
            InitialStartTimer = Config.Bind(
                "Loot Finder (Timing)",
                "Delay after spawn",
                6f,
                new ConfigDescription(
                    "Amount of seconds a bot will wait to start their first loot scan after spawning into raid.",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            LootScanInterval = Config.Bind(
                "Loot Finder (Timing)",
                "Loot scan interval",
                10f,
                new ConfigDescription(
                    "The amount of seconds the bot will wait until triggering another loot scan",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            TransactionDelay = Config.Bind(
                "Loot Finder (Timing)",
                "Transaction delay (ms)",
                500,
                new ConfigDescription(
                    "Amount of milliseconds a bot will wait after a looting transaction has occured before attempting another transaction. Simulates the amount of time it takes for a player to look through loot and equip things.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
        }

        public void LootSettings()
        {
            UseMarketPrices = Config.Bind(
                "Loot Settings",
                "Use flea market prices",
                false,
                new ConfigDescription(
                    "Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started",
                    null,
                    new ConfigurationManagerAttributes { Order = 11 }
                )
            );
            ValueFromMods = Config.Bind(
                "Loot Settings",
                "Calculate weapon value from attachments",
                true,
                new ConfigDescription(
                    "Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );
            CanStripAttachments = Config.Bind(
                "Loot Settings",
                "Allow weapon attachment stripping",
                true,
                new ConfigDescription(
                    "Allows bots to take the attachments off of a weapon if they are not able to pick the weapon up into their inventory",
                    null,
                    new ConfigurationManagerAttributes { Order = 9 }
                )
            );
            PMCMinLootThreshold = Config.Bind(
                "Loot Settings",
                "PMC: Min loot value threshold",
                12000f,
                new ConfigDescription(
                    "PMC bots will only loot items that exceed the specified value in roubles. When set to 0, bots will ignore the minimum value threshold",
                    null,
                    new ConfigurationManagerAttributes { Order = 8 }
                )
            );
            PMCMaxLootThreshold = Config.Bind(
                "Loot Settings",
                "PMC: Max loot value threshold",
                0f,
                new ConfigDescription(
                    "PMC bots will NOT loot items that exceed the specified value in roubles. When set to 0, bots will ignore the maximum value threshold",
                    null,
                    new ConfigurationManagerAttributes { Order = 7 }
                )
            );
            PMCGearToEquip = Config.Bind(
                "Loot Settings",
                "PMC: Allowed gear to equip",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a PMC bot is able to equip during raid",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            PMCGearToPickup = Config.Bind(
                "Loot Settings",
                "PMC: Allowed gear in bags",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a PMC bot is able to place in their backpack/rig",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            ScavMinLootThreshold = Config.Bind(
                "Loot Settings",
                "Scav: Min loot value threshold",
                5000f,
                new ConfigDescription(
                    "All non-PMC bots will only loot items that exceed the specified value in roubles. When set to 0, bots will ignore the minimum value threshold",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            ScavMaxLootThreshold = Config.Bind(
                "Loot Settings",
                "Scav: Max loot value threshold",
                0f,
                new ConfigDescription(
                    "All non-PMC bots will NOT loot items that exceed the specified value in roubles. When set to 0, bots will ignore the maximum value threshold",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            ScavGearToEquip = Config.Bind(
                "Loot Settings",
                "Scav: Allowed gear to equip",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a non-PMC bot is able to equip during raid",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            ScavGearToPickup = Config.Bind(
                "Loot Settings",
                "Scav: Allowed gear in bags",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a non-PMC bot is able to place in their backpack/rig",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );

            ItemAppraiserLogLevels = Config.Bind(
                "Loot Settings",
                "Log Levels",
                LogLevel.Error,
                new ConfigDescription(
                    "Enables logs for the item apprasier that calcualtes the weapon values",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
                )
            );
        }

        public void Awake()
        {
            LootFinderSettings();
            LootSettings();

            LootLog = new Log(Logger, LootingLogLevels);
            ItemAppraiserLog = new Log(Logger, ItemAppraiserLogLevels);

            new SettingsAndCachePatch().Enable();
            new RemoveComponent().Enable();

            BrainManager.RemoveLayer(
                "Utility peace",
                new List<string>()
                {
                    "Assault",
                    "ExUsec",
                    "BossSanitar",
                    "CursAssault",
                    "PMC",
                    "ArenaFighter",
                    "SectantWarrior"
                }
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>()
                {
                    "Assault",
                    "CursAssault",
                    "BossSanitar",
                    "BossKojaniy",
                    "BossGluhar",
                    "BirdEye",
                    "BigPipe",
                    "Knight",
                    "BossZryachiy",
                    "Tagilla",
                    "Killa",
                    "BossSanitar",
                    "BossBully",
                    "BossBoar",
                    "BoarSniper",
                    "FollowerGluharScout",
                    "FollowerGluharProtect",
                    "FollowerGluharAssault",
                    "Fl_Zraychiy",
                    "TagillaFollower",
                    "FollowerSanitar",
                    "FollowerBully",
                    "FlBoar"
                },
                2
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>() { "PMC", "ExUsec", "ArenaFighter" },
                3
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>() { "SectantWarrior" },
                13
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>() { "SectantPriest" },
                12
            );
            BrainManager.AddCustomLayer(typeof(LootingLayer), new List<string>() { "Obdolbs" }, 11);
        }

        public void Update()
        {
            bool shoultInitAppraiser =
                (!UseMarketPrices.Value && ItemAppraiser.HandbookData == null)
                || (UseMarketPrices.Value && !ItemAppraiser.MarketInitialized);

            // Initialize the itemAppraiser when the BE instance comes online
            if (
                Singleton<ClientApplication<ISession>>.Instance != null
                && Singleton<HandbookClass>.Instance != null
                && shoultInitAppraiser
            )
            {
                LootLog.LogInfo($"Initializing item appraiser");
                ItemAppraiser.Init();
            }
        }
    }
}
