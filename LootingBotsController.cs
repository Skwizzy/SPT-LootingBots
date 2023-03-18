using BepInEx;
using BepInEx.Configuration;
using System;
using LootingBots.Patch;

namespace LootingBots
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class LootingBots : BaseUnityPlugin
    {
        private const String MOD_GUID = "me.skwizzy.lootingbots";
        private const String MOD_NAME = "LootingBots";
        private const String MOD_VERSION = "0.1";

        public static ConfigEntry<bool> enableLogging;

        public static ConfigEntry<bool> pmcLootingEnabled;
        public static ConfigEntry<float> bodySeeDist;
        public static ConfigEntry<float> bodyLeaveDist;
        public static ConfigEntry<float> bodyLookPeriod;

        public void Awake()
            {
                pmcLootingEnabled = Config.Bind("Corpse Loot Settings", "PMCs can loot (experimental)", false, "Allows PMC bots to loot corpses. Experimental: poor behavior with weapon swapping currently as they will always use scav weapons when picked up");
                bodySeeDist = Config.Bind("Corpse Loot Settings", "Distance to see body", 25f, "If the bot is with X meters, it can see the body");
                bodyLeaveDist = Config.Bind("Corpse Loot Settings", "Distance to forget body", 50f, "If the bot is further than X meters, it will forget about the body");
                bodyLookPeriod = Config.Bind("Corpse Loot Settings", "Looting time (warning)", 8.0f, "Time bot stands at corpse looting. Shorter times may display strange behavior");
                enableLogging = Config.Bind(
                "Corpse Loot Settings",
                "Enable Debug",
                false,
                "Enables log messages to be printed");

                new CorpseLootSettingsPatch().Enable();
                new LootCorpsePatch().Enable();
            }
    }
}