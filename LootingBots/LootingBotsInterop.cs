using BepInEx.Bootstrap;

using EFT;
using EFT.Interactive;

using HarmonyLib;

using System;
using System.Reflection;

namespace LootingBots
{
    internal static class LootingBotsInterop
    {
        private static bool _LootingBotsLoadedChecked = false;
        private static bool _LootingBotsInteropInited = false;

        private static bool _IsLootingBotsLoaded;
        private static Type _LootingBotsExternalType;
        private static MethodInfo _ForceBotToScanLootMethod;
        private static MethodInfo _PreventBotFromLootingMethod;
        private static MethodInfo _CheckIfInventoryFullMethod;
        private static MethodInfo _GetNetLootValueMethod;
        private static MethodInfo _GetItemPriceMethod;

        /**
         * Return true if Looting Bots is loaded in the client
         */
        public static bool IsLootingBotsLoaded()
        {
            // Only check for SAIN once
            if (!_LootingBotsLoadedChecked)
            {
                _LootingBotsLoadedChecked = true;
                _IsLootingBotsLoaded = Chainloader.PluginInfos.ContainsKey(
                    "me.skwizzy.lootingbots"
                );
            }

            return _IsLootingBotsLoaded;
        }

        /**
         * Initialize the Looting Bots interop class data, return true on success
         */
        public static bool Init()
        {
            if (!IsLootingBotsLoaded())
                return false;

            // Only check for the External class once
            if (!_LootingBotsInteropInited)
            {
                _LootingBotsInteropInited = true;

                _LootingBotsExternalType = Type.GetType(
                    "LootingBots.External, skwizzy.LootingBots"
                );

                // Only try to get the methods if we have the type
                if (_LootingBotsExternalType != null)
                {
                    _ForceBotToScanLootMethod = AccessTools.Method(
                        _LootingBotsExternalType,
                        "ForceBotToScanLoot"
                    );
                    _PreventBotFromLootingMethod = AccessTools.Method(
                        _LootingBotsExternalType,
                        "PreventBotFromLooting"
                    );
                    _CheckIfInventoryFullMethod = AccessTools.Method(
                        _LootingBotsExternalType,
                        "CheckIfInventoryFull"
                    );
                    _GetNetLootValueMethod = AccessTools.Method(
                        _LootingBotsExternalType,
                        "GetNetLootValue"
                    );
                    _GetItemPriceMethod = AccessTools.Method(
                        _LootingBotsExternalType,
                        "GetItemPrice"
                    );
                }
            }

            // If we found the External class, at least some of the methods are (probably) available
            return _LootingBotsExternalType != null;
        }

        /**
         * Force a bot to search for loot immediately if Looting Bots is loaded. Return true if successful.
         */
        public static bool TryForceBotToScanLoot(BotOwner botOwner)
        {
            if (!Init())
                return false;
            if (_ForceBotToScanLootMethod == null)
                return false;

            return (bool)_ForceBotToScanLootMethod.Invoke(null, new object[] { botOwner });
        }

        /**
         * Stops a bot from looting and searching for loot (until the scan timer expires) if Looting Bots is loaded. Return true if successful.
         */
        public static bool TryPreventBotFromLooting(BotOwner botOwner, float duration)
        {
            if (!Init())
                return false;
            if (_PreventBotFromLootingMethod == null)
                return false;

            return (bool)
                _PreventBotFromLootingMethod.Invoke(null, new object[] { botOwner, duration });
        }

        /**
         * Checks if a bot's inventory is full or not
         */
        public static bool CheckIfInventoryFull(BotOwner botOwner)
        {
            if (!Init())
                return false;
            if (_CheckIfInventoryFullMethod == null)
                return false;

            return (bool)
                _CheckIfInventoryFullMethod.Invoke(null, new object[] { botOwner });
        }

        /**
         * Gets the total value looted by a bot in this raid
         */
        public static float GetNetLootValue(BotOwner botOwner)
        {
            if (!Init())
                return 0f;
            if (_GetNetLootValueMethod == null)
                return 0f;

            return (float)
                _GetNetLootValueMethod.Invoke(null, new object[] { botOwner });
        }

        /**
         * Checks the price of a loot item using LB ItemAppraiser
         */
        public static float GetItemPrice(LootItem item)
        {
            if (!Init())
                return 0f;
            if (_GetItemPriceMethod == null)
                return 0f;

            return (float)
                _GetItemPriceMethod.Invoke(null, new object[] { item });
        }
    }
}
