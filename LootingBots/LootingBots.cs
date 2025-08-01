using BepInEx;
using BepInEx.Configuration;

using Comfort.Common;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Components;
using LootingBots.Patches;
using LootingBots.Utilities;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInDependency("xyz.drakia.bigbrain", "1.3.2")]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const string MOD_GUID = "me.skwizzy.lootingbots";
        private const string MOD_NAME = "LootingBots";
        private const string MOD_VERSION = "1.6.0";

        public const BotType SettingsDefaults = BotType.Scav | BotType.Pmc | BotType.PlayerScav | BotType.Raider;

        public const EquipmentType CanPickupEquipmentDefaults =
            EquipmentType.ArmoredRig
            | EquipmentType.ArmorVest
            | EquipmentType.Backpack
            | EquipmentType.Grenade
            | EquipmentType.Helmet
            | EquipmentType.TacticalRig
            | EquipmentType.Weapon
            | EquipmentType.Dogtag;

        // Loot Finder Settings
        public static ConfigEntry<BotType> CorpseLootingEnabled;
        public static ConfigEntry<BotType> ContainerLootingEnabled;
        public static ConfigEntry<BotType> LooseItemLootingEnabled;
        public static ConfigEntry<float> InitialStartTimer;

        public static ConfigEntry<float> LootScanInterval;
        public static ConfigEntry<float> DetectItemDistance;
        public static ConfigEntry<bool> DetectItemNeedsSight;
        public static ConfigEntry<float> DetectContainerDistance;
        public static ConfigEntry<bool> DetectContainerNeedsSight;
        public static ConfigEntry<float> DetectCorpseDistance;
        public static ConfigEntry<bool> DetectCorpseNeedsSight;

        public static ConfigEntry<bool> DebugLootNavigation;
        public static ConfigEntry<LogLevel> LootingLogLevels;
        public static ConfigEntry<LogLevel> InteropLogLevels;

        public static ConfigEntry<int> FilterLogsOnBot;
        public static Log LootLog;
        public static Log InteropLog;

        // Loot Settings
        public static ConfigEntry<bool> BotsAlwaysCloseContainers;
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<int> TransactionDelay;
        public static ConfigEntry<bool> UseExamineTime;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<bool> CanStripAttachments;

        public static ConfigEntry<float> PMCMinLootThreshold;
        public static ConfigEntry<float> PMCMaxLootThreshold;
        public static ConfigEntry<float> ScavMinLootThreshold;
        public static ConfigEntry<float> ScavMaxLootThreshold;

        public static ConfigEntry<CanEquipEquipmentType> PMCGearToEquip;
        public static ConfigEntry<EquipmentType> PMCGearToPickup;
        public static ConfigEntry<CanEquipEquipmentType> ScavGearToEquip;
        public static ConfigEntry<EquipmentType> ScavGearToPickup;

        public static ConfigEntry<LogLevel> ItemAppraiserLogLevels;
        public static Log ItemAppraiserLog;
        public static ItemAppraiser ItemAppraiser { get; private set; } = new ItemAppraiser();

        // Performance Settings
        public static ConfigEntry<int> MaxActiveLootingBots;
        public static ConfigEntry<int> LimitDistanceFromPlayer;

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
            DetectCorpseNeedsSight = Config.Bind(
                "Loot Finder",
                "Enable corpse line of sight check",
                false,
                new ConfigDescription(
                    "When scanning for loot, corpses will be ignored if they are not visible by the bot",
                    null,
                    new ConfigurationManagerAttributes { Order = 9 }
                )
            );
            DetectCorpseDistance = Config.Bind(
                "Loot Finder",
                "Detect corpse distance",
                80f,
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
            DetectContainerNeedsSight = Config.Bind(
                "Loot Finder",
                "Enable container line of sight check",
                false,
                new ConfigDescription(
                    "When scanning for loot, containers will be ignored if they are not visible by the bot",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            DetectContainerDistance = Config.Bind(
                "Loot Finder",
                "Detect container distance",
                80f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );

            LooseItemLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable loose item looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables loose item looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            DetectItemNeedsSight = Config.Bind(
                "Loot Finder",
                "Enable item line of sight check",
                false,
                new ConfigDescription(
                    "When scanning for loot, loose items will be ignored if they are not visible by the bot",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            DetectItemDistance = Config.Bind(
                "Loot Finder",
                "Detect item distance",
                80f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect an item",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );

            LootingLogLevels = Config.Bind(
                "Loot Finder",
                "Debug: Log Levels",
                LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 0, IsAdvanced = true }
                )
            );
            InteropLogLevels = Config.Bind(
                "Loot Finder",
                "Debug: Interop Log Levels",
                LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages specific to the mod interop methods",
                    null,
                    new ConfigurationManagerAttributes { Order = -1, IsAdvanced = true }
                )
            );
            FilterLogsOnBot = Config.Bind(
                "Loot Finder",
                "Debug: Filter logs on bot",
                0,
                new ConfigDescription(
                    "Filters new log entries only showing logs for the number of the bot specified. A value of 0 denotes no filter",
                    null,
                    new ConfigurationManagerAttributes { Order = -2, IsAdvanced = true }
                )
            );
            DebugLootNavigation = Config.Bind(
                "Loot Finder",
                "Debug: Show navigation points",
                false,
                new ConfigDescription(
                    "Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Black): 'Optimized' container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).",
                    null,
                    new ConfigurationManagerAttributes { Order = -3, IsAdvanced = true }
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
                "Delay after taking item (ms)",
                500,
                new ConfigDescription(
                    "Amount of milliseconds a bot will wait after taking an item into their inventory before attempting to loot another item. Simulates the amount of time it takes for a player to look through loot decide to take something.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            UseExamineTime = Config.Bind(
                "Loot Finder (Timing)",
                "Enable examine time",
                true,
                new ConfigDescription(
                    "Adds a delay before looting an item to simulate the time it takes for a bot to \"uncover (examine)\" an item when searching containers, items and corpses. The delay is calculated using the ExamineTime of an object and the AttentionExamineTime of the bot.",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
                )
            );
        }

        public void LootSettings()
        {
            BotsAlwaysCloseContainers = Config.Bind(
                "Loot Settings",
                "Bots always close containers",
                true,
                new ConfigDescription(
                    "When enabled, bots will always try to close a container after they have finished looting. If the bot is inturrupted while looting, the container may remain open.",
                    null,
                    new ConfigurationManagerAttributes { Order = 12 }
                )
            );
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
                CanEquipEquipmentType.All,
                new ConfigDescription(
                    "The equipment a PMC bot is able to equip during raid",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            PMCGearToPickup = Config.Bind(
                "Loot Settings",
                "PMC: Allowed gear in bags",
                CanPickupEquipmentDefaults,
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
                CanEquipEquipmentType.All,
                new ConfigDescription(
                    "The equipment a non-PMC bot is able to equip during raid",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            ScavGearToPickup = Config.Bind(
                "Loot Settings",
                "Scav: Allowed gear in bags",
                CanPickupEquipmentDefaults,
                new ConfigDescription(
                    "The equipment a non-PMC bot is able to place in their backpack/rig",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );

            ItemAppraiserLogLevels = Config.Bind(
                "Loot Settings",
                "Debug: Item Appraiser Log Levels",
                LogLevel.Error,
                new ConfigDescription(
                    "Enables logs for the item apprasier that calcualtes the weapon values",
                    null,
                    new ConfigurationManagerAttributes { Order = 0, IsAdvanced = true }
                )
            );
        }

        public void PerformanceSettings()
        {
            MaxActiveLootingBots = Config.Bind(
                "Performance",
                "Maximum looting bots",
                20,
                new ConfigDescription(
                    "Limits the amount of bots that are able to simultaneously run looting logic. A value of 0 represents no limit",
                    null,
                    new ConfigurationManagerAttributes { Order = 11 }
                )
            );
            LimitDistanceFromPlayer = Config.Bind(
                "Performance",
                "Limit looting by distance to player",
                0,
                new ConfigDescription(
                    "Any bot farther than the specified distance in meters will not run any looting logic. A value of 0 represents no limit",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );
        }

        public void Awake()
        {
            LootFinderSettings();
            LootSettings();
            PerformanceSettings();

            LootLog = new Log(Logger, LootingLogLevels);
            InteropLog = new Log(Logger, InteropLogLevels);
            ItemAppraiserLog = new Log(Logger, ItemAppraiserLogLevels);

            new RemoveLootingBrainPatch().Enable();
            new CleanCacheOnRaidEndPatch().Enable();
            new EnableWeaponSwitchingPatch().Enable();

            BrainManager.RemoveLayer(
                "Utility peace",
                [
                    "Assault",
                    "ExUsec",
                    "BossSanitar",
                    "CursAssault",
                    "PMC",
                    "PmcUsec",
                    "PmcBear",
                    "ExUsec",
                    "ArenaFighter",
                    "SectantWarrior",
                ]
            );

            // Remove BSG's own looting layer
            BrainManager.RemoveLayer("LootPatrol", ["Assault", "PMC"]);

            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                [
                    "Assault",
                    "CursAssault",
                    "BossSanitar",
                    "BossKojaniy",
                    "BossGluhar",
                    "BossPartisan",
                    "BossKolontay",
                    "BirdEye",
                    "BigPipe",
                    "Knight",
                    "Tagilla",
                    "Killa",
                    "BossSanitar",
                    "BossBully",
                    "BossBoar",
                    "FollowerGluharScout",
                    "FollowerGluharProtect",
                    "FollowerGluharAssault",
                    "Fl_Zraychiy",
                    "TagillaFollower",
                    "KolonSec",
                    "FollowerSanitar",
                    "FollowerBully",
                    "FlBoar",
                ],
                4
            );

            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                ["PMC", "PmcUsec", "PmcBear", "ExUsec", "ArenaFighter"],
                5
            );

            BrainManager.AddCustomLayer(typeof(LootingLayer), ["SectantWarrior"], 13);

            BrainManager.AddCustomLayer(typeof(LootingLayer), ["SectantPriest"], 13);

            BrainManager.AddCustomLayer(typeof(LootingLayer), ["Obdolbs"], 11);
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
