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

using HandbookClass = GClass2773;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInDependency("xyz.drakia.bigbrain", "0.1.4")]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const string MOD_GUID = "me.skwizzy.lootingbots";
        private const string MOD_NAME = "LootingBots";
        private const string MOD_VERSION = "1.1.1";

        public const BotType SettingsDefaults = BotType.Scav | BotType.Pmc | BotType.Raider;

        // Loot Finder Settings
        public static ConfigEntry<BotType> CorpseLootingEnabled;
        public static ConfigEntry<BotType> ContainerLootingEnabled;
        public static ConfigEntry<BotType> LooseItemLootingEnabled;

        public static ConfigEntry<float> TimeToWaitBetweenLoot;
        public static ConfigEntry<float> DetectItemDistance;
        public static ConfigEntry<float> DetectContainerDistance;
        public static ConfigEntry<float> DetectCorpseDistance;

        public static ConfigEntry<bool> DebugLootNavigation;
        public static ConfigEntry<LogLevel> LootingLogLevels;
        public static Log LootLog;

        // Loot Settings
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<bool> CanStripAttachments;
        public static ConfigEntry<float> PMCLootThreshold;
        public static ConfigEntry<float> ScavLootThreshold;
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
                    new ConfigurationManagerAttributes { Order = 9 }
                )
            );
            ContainerLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable container looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables container looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 8 }
                )
            );
            DetectContainerDistance = Config.Bind(
                "Loot Finder",
                "Detect container distance",
                75f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container",
                    null,
                    new ConfigurationManagerAttributes { Order = 7 }
                )
            );
            LooseItemLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable loose item looting",
                SettingsDefaults,
                new ConfigDescription(
                    "Enables loose item looting for the selected bot types",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            DetectItemDistance = Config.Bind(
                "Loot Finder",
                "Detect item distance",
                75f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect an item",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            TimeToWaitBetweenLoot = Config.Bind(
                "Loot Finder",
                "Delay between looting",
                15f,
                new ConfigDescription(
                    "The amount of time the bot will wait after looting an container/item/corpse before trying to find the next nearest item/container/corpse",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            LootingLogLevels = Config.Bind(
                "Loot Finder",
                "Log Levels",
                LogLevel.Error | LogLevel.Info,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            DebugLootNavigation = Config.Bind(
                "Loot Finder",
                "Debug: Show navigation points",
                false,
                new ConfigDescription(
                    "Renders shperes where bots are trying to navigate when container looting. (Red): Container position. (Black): 'Optimized' container position. (Green): Calculated bot destination. (Blue): NavMesh corrected destination (where the bot will move).",
                    null,
                    new ConfigurationManagerAttributes { Order = 0 }
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
                    new ConfigurationManagerAttributes { Order = 10 }
                )
            );
            ValueFromMods = Config.Bind(
                "Loot Settings",
                "Calculate weapon value from attachments",
                true,
                new ConfigDescription(
                    "Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check",
                    null,
                    new ConfigurationManagerAttributes { Order = 9 }
                )
            );
            CanStripAttachments = Config.Bind(
                "Loot Settings",
                "Allow weapon attachment stripping",
                true,
                new ConfigDescription(
                    "Allows bots to take the attachments off of a weapon if they are not able to pick the weapon up into their inventory",
                    null,
                    new ConfigurationManagerAttributes { Order = 8 }
                )
            );
            PMCLootThreshold = Config.Bind(
                "Loot Settings",
                "PMC: Loot value threshold",
                12000f,
                new ConfigDescription(
                    "PMC bots will only loot items that exceed the specified value in roubles",
                    null,
                    new ConfigurationManagerAttributes { Order = 6 }
                )
            );
            PMCGearToEquip = Config.Bind(
                "Loot Settings",
                "PMC: Allowed gear to equip",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a PMC bot is able to equip during raid",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            PMCGearToPickup = Config.Bind(
                "Loot Settings",
                "PMC: Allowed gear in bags",
                EquipmentType.All,
                new ConfigDescription(
                    "The equipment a PMC bot is able to place in their backpack/rig",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            ScavLootThreshold = Config.Bind(
                "Loot Settings",
                "Scav: Loot value threshold",
                5000f,
                new ConfigDescription(
                    "All non-PMC bots will only loot items that exceed the specified value in roubles.",
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

            BrainManager.RemoveLayer(
                "Utility peace",
                new List<string>()
                {
                    "Assault",
                    "ExUsec",
                    "BossSanitar",
                    "CursAssault",
                    "PMC",
                    "SectantWarrior"
                }
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>()
                {
                    "Assault",
                    "BossSanitar",
                    "CursAssault",
                    "BossKojaniy",
                    "SectantPriest",
                    "FollowerGluharScout",
                    "FollowerGluharProtect",
                    "FollowerGluharAssault",
                    "BossGluhar",
                    "Fl_Zraychiy",
                    "TagillaFollower",
                    "FollowerSanitar",
                    "FollowerBully",
                    "BirdEye",
                    "BigPipe",
                    "Knight",
                    "BossZryachiy",
                    "Tagilla",
                    "Killa",
                    "BossSanitar",
                    "BossBully"
                },
                2
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>() { "PMC", "ExUsec" },
                3
            );
            BrainManager.AddCustomLayer(
                typeof(LootingLayer),
                new List<string>() { "SectantWarrior" },
                13
            );
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
