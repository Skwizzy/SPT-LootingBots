using Comfort.Common;

using EFT;

namespace LootingBots.Utilities
{
    /// <summary>
    /// Tracks lootable objects currently targeted by bots to prevent multiple bots
    /// from navigating to the same lootable simultaneously.
    /// </summary>
    public static class ActiveLootCache
    {
        // Handle to the players instance for use in friendly checks
        public static List<IPlayer> ActivePlayers { get; private set; } = [];

        public static Dictionary<string, BotOwner> ActiveLoot { get; private set; } = [];

        public static void Init()
        {
            if (ActivePlayers.Count > 0)
            {
                return;
            }

            foreach (var player in Singleton<GameWorld>.Instance.RegisteredPlayers)
            {
                if (player.IsAI)
                {
                    continue;
                }

                if (!player.HealthController.IsAlive)
                {
                    continue;
                }

                ActivePlayers.Add(player);
            }
        }

        public static void Reset()
        {
            ActiveLoot = [];
            ActivePlayers = [];
        }

        public static void CacheActiveLootId(string containerId, BotOwner botOwner)
        {
            if (!string.IsNullOrEmpty(botOwner.name))
            {
                ActiveLoot.Add(containerId, botOwner);
            }
        }

        public static bool IsLootInUse(string lootId)
        {
            return ActiveLoot.TryGetValue(lootId, out BotOwner _);
        }

        public static void Cleanup(BotOwner botOwner)
        {
            try
            {
                // Check to make sure the BotOwner we are cleaning up has a valid name
                if (botOwner == null || botOwner.name == null)
                {
                    if (LootingBots.LootLog.ErrorEnabled)
                    {
                        LootingBots.LootLog.LogError("Cleanup issued on a bot with no name?");
                    }
                    return;
                }

                List<string> keysToRemove = [];

                // Look through the entries in the dictionary and remove any that match the specified bot owner
                foreach (KeyValuePair<string, BotOwner> keyValue in ActiveLoot)
                {
                    // Check to make sure the BotOwner saved in the dictionary has a valid name before comparing
                    if (keyValue.Value == null || keyValue.Value.name == null)
                    {
                        if (LootingBots.LootLog.ErrorEnabled)
                        {
                            LootingBots.LootLog.LogError("Bot in loot cache has no name?");
                        }
                        continue;
                    }

                    // If the bot's name matches, remove the item
                    if (keyValue.Value.name == botOwner.name)
                    {
                        keysToRemove.Add(keyValue.Key);
                    }
                }

                foreach(string key in keysToRemove)
                {
                    ActiveLoot.Remove(key);
                }
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e);
            }
        }
    }
}
