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

        public void Awake()
            {
                enableLogging = Config.Bind(
                "Settings",
                "Enable Debug",
                false,
                "Enables log messages to be printed");
                
                new LootCorpsePatch().Enable();
            }
    }
}