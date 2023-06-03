using System;
using System.Collections.Generic;

namespace LootingBots.Patch.Util
{
    // Cached used to keep track of what lootable are currently being targeted by a bot so that multiple bots
    // dont try and path to the same lootable
    public static class ActiveLootCache
    {
        public static Dictionary<string, int> ActiveLoot = new Dictionary<string, int>();

        public static void Reset()
        {
            ActiveLoot = new Dictionary<string, int>();
        }

        public static void CacheActiveLootId(string containerId, int botId)
        {
            ActiveLoot.Add(containerId, botId);
        }

        public static bool IsLootInUse(string containerId)
        {
            return ActiveLoot.TryGetValue(containerId, out int _);
        }

        public static void Cleanup(string lootId)
        {
            try
            {
                ActiveLoot.Remove(lootId);
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e.StackTrace);
            }
        }
    }
}
