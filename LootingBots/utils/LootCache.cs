using System;
using System.Collections.Generic;
using System.Linq;

using EFT;

namespace LootingBots.Patch.Util
{
    // Cached used to keep track of what lootable are currently being targeted by a bot so that multiple bots
    // dont try and path to the same lootable
    public static class ActiveLootCache
    {
        public static string PlayerLootId;

        public static Dictionary<string, BotOwner> ActiveLoot = new Dictionary<string, BotOwner>();

        public static void Reset()
        {
            ActiveLoot = new Dictionary<string, BotOwner>();
        }

        public static void CacheActiveLootId(string containerId, BotOwner botOwner)
        {
            ActiveLoot.Add(containerId, botOwner);
        }

        public static bool IsLootInUse(string lootId)
        {
            return lootId == PlayerLootId || ActiveLoot.TryGetValue(lootId, out BotOwner _);
        }

        public static void Cleanup(BotOwner botOwner)
        {
            try
            {
                // Look through the entries in the disctionary and remove any that match the specified bot owner
                foreach (
                    var item in ActiveLoot
                        .Where(keyValue => keyValue.Value.name == botOwner.name)
                        .ToList()
                )
                {
                    ActiveLoot.Remove(item.Key);
                }
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e);
            }
        }
    }
}
