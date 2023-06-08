using BepInEx;
using BepInEx.Configuration;

using Comfort.Common;

using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;
using LootingBots.Brain;

using DrakiaXYZ.BigBrain.Brains;
using System.Collections.Generic;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const string MOD_GUID = "me.skwizzy.lootingbots";
        private const string MOD_NAME = "LootingBots";
        private const string MOD_VERSION = "1.1.0";

        // Loot Finder
        public static ConfigEntry<BotType> ContainerLootingEnabled;
        public static ConfigEntry<BotType> LooseItemLootingEnabled;

        public static ConfigEntry<float> TimeToWaitBetweenLoot;
        public static ConfigEntry<float> DetectLootDistance;
        public static ConfigEntry<bool> DebugLootNavigation;
        public static ConfigEntry<LogUtils.LogLevel> LootingLogLevels;
        public static ConfigEntry<bool> UseMarketPrices;
        public static ConfigEntry<bool> ValueFromMods;
        public static ConfigEntry<LogUtils.LogLevel> ItemAppraiserLogLevels;
        public static Log LootLog;
        public static Log ItemAppraiserLog;

        // Corpse Looting
        public static ConfigEntry<float> BodySeeDist;
        public static ConfigEntry<float> BodyLeaveDist;
        public static ConfigEntry<float> BodyLookPeriod;
        public static ConfigEntry<BotType> CorpseLootingEnabled;
        public static ItemAppraiser ItemAppraiser = new ItemAppraiser();

        public void LootFinderSettings()
        {
            ContainerLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable container looting",
                BotType.All,
                new ConfigDescription(
                    "Enable dynamic looting of containers, will detect containers within the set distance and navigate to them similar to how they would loot a corpse",
                    null,
                    new ConfigurationManagerAttributes { Order = 5 }
                )
            );
            LooseItemLootingEnabled = Config.Bind(
                "Loot Finder",
                "Enable loose item looting",
                BotType.All,
                new ConfigDescription(
                    "Enable dynamic looting of loose items, will detect items within the set distance and navigate to them similar to how they would loot a corpse",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 }
                )
            );
            TimeToWaitBetweenLoot = Config.Bind(
                "Loot Finder",
                "Delay between looting",
                45f,
                new ConfigDescription(
                    "The amount of time the bot will wait after looting an item/container before trying to find the next nearest item/container",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 }
                )
            );
            DetectLootDistance = Config.Bind(
                "Loot Finder",
                "Detect loot distance",
                25f,
                new ConfigDescription(
                    "Distance (in meters) a bot is able to detect a container/item",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            LootingLogLevels = Config.Bind(
                "Loot Finder",
                "Log Levels",
                LogUtils.LogLevel.Error,
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

        public void CorpseLootSettings()
        {
            CorpseLootingEnabled = Config.Bind(
                "Corpse Looting",
                "Enable corpse looting",
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
                    new ConfigurationManagerAttributes { Order = 2 }
                )
            );
            ValueFromMods = Config.Bind(
                "Weapon Looting",
                "Calculate value from attachments",
                true,
                new ConfigDescription(
                    "Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check. Disable if experiencing performance issues",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );
            ItemAppraiserLogLevels = Config.Bind(
                "Weapon Looting",
                "Log Levels",
                LogUtils.LogLevel.Error,
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
            CorpseLootSettings();
            WeaponLootSettings();

            LootLog = new Log(Logger, LootingLogLevels);
            ItemAppraiserLog = new Log(Logger, ItemAppraiserLogLevels);

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
                    "ExUsec",
                    "BossSanitar",
                    "CursAssault",
                    "PMC",
                    "SectantWarrior",
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
                    "BossZryachiy",
                    "Tagilla",
                    "BossSanitar",
                    "BossBully"
                },
                2
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
                && Singleton<GClass2531>.Instance != null
                && shoultInitAppraiser
            )
            {
                LootLog.LogInfo($"Initializing item appraiser");
                ItemAppraiser.Init();
            }
        }
    }
}
