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

        public static ConfigEntry<LogUtils.LogLevel> enabledLogLevels;

        public static ConfigEntry<bool> pmcLootingEnabled;
        public static ConfigEntry<float> bodySeeDist;
        public static ConfigEntry<float> bodyLeaveDist;
        public static ConfigEntry<float> bodyLookPeriod;
        public static ConfigEntry<bool> useMarketPrices;
        public static ConfigEntry<bool> valueFromMods;
        public static ConfigEntry<BotType> lootingEnabledBots;
        public static Log log;
        public static ItemAppraiser itemAppraiser = new ItemAppraiser();

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
                log.logWarning($"Initializing item appraiser");
                itemAppraiser.init();
            }
        }

        public void Awake()
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
            enabledLogLevels = Config.Bind<LogUtils.LogLevel>(
                "Corpse Looting",
                "Log Levels",
                LogUtils.LogLevel.Error,
                new ConfigDescription(
                    "Enable different levels of log messages to show in the logs",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 }
                )
            );

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

            log = new Log(Logger);
            new LootContainerPatch().Enable();
            // new LootContainerPatch1().Enable();
            new CorpseLootSettingsPatch().Enable();
            new LootCorpsePatch().Enable();
        }
    }
}
