using System;

using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots
{
    public static class External
    {
        /** Forces a bot to scan for loot as soon as they are able to. */
        public static bool ForceBotToScanLoot(BotOwner bot)
        {
            if (GetAllComponents(bot, out LootingBrain lootingBrain, out LootFinder lootFinder))
            {
                BotLog log = new BotLog(LootingBots.LootLog, bot);

                if (!lootingBrain.HasFreeSpace)
                {
                    if (log.WarningEnabled)
                        log.LogWarning("Forcing a scan but bot does not have enough free space");
                }
                else if (log.DebugEnabled)
                {
                    log.LogDebug("Forcing a loot scan");
                }

                lootFinder.ScanTimer = Time.time - 1f;
                lootFinder.LockUntilNextScan = true;
                return true;
            }
            return false;
        }

        /** Stops a bot from looting if it is currently looting something and prevents loot scans for the amount of seconds specified by duration */
        public static bool PreventBotFromLooting(BotOwner bot, float duration)
        {
            if (GetAllComponents(bot, out LootingBrain lootingBrain, out LootFinder lootFinder))
            {
                BotLog log = new BotLog(LootingBots.LootLog, bot);

                if (log.DebugEnabled)
                    log.LogDebug($"Preventing a bot from looting for the next {duration} seconds");

                lootFinder.ScanTimer = Time.time + duration;
                lootFinder.LockUntilNextScan = true;
                lootingBrain.DisableTransactions();
                return true;
            }
            return false;
        }

        /**
         * Checks if a bot's inventory is full or not
         */
        public static bool CheckIfInventoryFull(BotOwner bot)
        {
            if (GetLootingBrain(bot, out LootingBrain lootingBrain))
            {
                BotLog log = new BotLog(LootingBots.LootLog, bot);

                if (log.DebugEnabled)
                    log.LogDebug(
                        $"Checking if {bot.name} has Free Space in their inventory. Result: {lootingBrain.HasFreeSpace}"
                    );

                return !lootingBrain.HasFreeSpace;
            }
            return false;
        }

        /**
         * Gets the total value looted by a bot in this raid
         */
        public static float GetNetLootValue(BotOwner bot)
        {
            if (GetLootingBrain(bot, out LootingBrain lootingBrain))
            {
                BotLog log = new BotLog(LootingBots.LootLog, bot);

                if (log.DebugEnabled)
                    log.LogDebug(
                        $"Getting Net Loot Value for {bot.name} which is {lootingBrain.Stats.NetLootValue}"
                    );

                return lootingBrain.Stats.NetLootValue;
            }
            return 0f;
        }

        /**
         * Checks the price of a loot item using LB ItemAppraiser
         */
        public static float GetItemPrice(Item item)
        {
            return LootingBots.ItemAppraiser != null
                ? LootingBots.ItemAppraiser.GetItemPrice(item)
                : 0;
        }

        private static bool GetAllComponents(
            BotOwner bot,
            out LootingBrain lootingBrain,
            out LootFinder lootFinder
        )
        {
            bool hasLootFinder = GetLootFinder(bot, out lootFinder);
            bool hasLootingBrain = GetLootingBrain(bot, out lootingBrain);
            return hasLootingBrain && hasLootFinder;
        }

        private static bool GetLootingBrain(BotOwner bot, out LootingBrain lootingBrain)
        {
            lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            return lootingBrain != null;
        }

        private static bool GetLootFinder(BotOwner bot, out LootFinder lootFinder)
        {
            lootFinder = bot.GetPlayer.gameObject.GetComponent<LootFinder>();
            return lootFinder != null;
        }
    }
}
