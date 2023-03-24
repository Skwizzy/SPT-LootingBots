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

        public static ConfigEntry<bool> enableLogging;

        public static ConfigEntry<bool> pmcLootingEnabled;
        public static ConfigEntry<float> bodySeeDist;
        public static ConfigEntry<float> bodyLeaveDist;
        public static ConfigEntry<float> bodyLookPeriod;
        public static ConfigEntry<bool> useMarketPrices;
        public static ConfigEntry<bool> valueFromMods;
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
            pmcLootingEnabled = Config.Bind(
                "Corpse Looting",
                "PMCs can loot",
                true,
                "Allows PMC bots to loot corpses"
            );
            bodySeeDist = Config.Bind(
                "Corpse Looting",
                "Distance to see body",
                25f,
                "If the bot is with X meters, it can see the body"
            );
            bodyLeaveDist = Config.Bind(
                "Corpse Looting",
                "Distance to forget body",
                50f,
                "If the bot is further than X meters, it will forget about the body"
            );
            bodyLookPeriod = Config.Bind(
                "Corpse Looting",
                "Looting time (*)",
                8.0f,
                "Time bot stands at corpse looting. *WARNING: Shorter times may display strange behavior"
            );
            enableLogging = Config.Bind(
                "Corpse Looting",
                "Enable Debug",
                false,
                "Enables log messages to be printed"
            );

            useMarketPrices = Config.Bind(
                "Weapon Looting",
                "Use flea market prices",
                false,
                "Bots will query more accurate ragfair prices to do item value checks. Will make a query to get ragfair prices when the client is first started. May affect initial client start times."
            );
            valueFromMods = Config.Bind(
                "Weapon Looting",
                "Calculate value from attachments",
                true,
                "Calculate weapon value by looking up each attachement. More accurate than just looking at the base weapon template but a slightly more expensive check. Disable if experiencing performance issues"
            );

            log = new Log(Logger);
            new CorpseLootSettingsPatch().Enable();
            new LootCorpsePatch().Enable();
        }
    }
}
