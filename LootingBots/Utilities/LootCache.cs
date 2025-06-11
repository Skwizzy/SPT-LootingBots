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
        // Id of container/corpse that the player is currently looting
        public static string PlayerLootId { get; set; }

        // Handle to the players intance for use in friendly checks
        public static Player MainPlayer { get; set; }

        public static Dictionary<string, BotOwner> ActiveLoot { get; private set; } = [];

        public static void Init()
        {
            if (!MainPlayer)
            {
                MainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
            }
        }

        public static void Reset()
        {
            ActiveLoot = [];
            PlayerLootId = "";
            MainPlayer = null;
        }

        public static void CacheActiveLootId(string containerId, BotOwner botOwner)
        {
            if (!string.IsNullOrEmpty(botOwner.name))
            {
                ActiveLoot.Add(containerId, botOwner);
            }
        }

        public static bool IsLootInUse(string lootId, BotOwner botOwner)
        {
            bool isFriendly = !botOwner.BotsGroup.IsPlayerEnemy(MainPlayer);
            return isFriendly && lootId == PlayerLootId
                || ActiveLoot.TryGetValue(lootId, out BotOwner _);
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

                // Look through the entries in the dictionary and remove any that match the specified bot owner
                foreach (KeyValuePair<string, BotOwner> keyValue in ActiveLoot.ToList())
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
                        ActiveLoot.Remove(keyValue.Key);
                    }
                }
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e);
            }
        }
    }
}