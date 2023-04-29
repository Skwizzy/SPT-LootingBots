using System;
using System.Collections.Generic;
using System.Linq;

using EFT;
using EFT.Interactive;

using LootingBots.Patch.Components;

using UnityEngine;

namespace LootingBots.Patch.Util
{
    public class BotLootData
    {
        public LootFinder LootFinder;

        // Current container that the bot will try to loot
        public LootableContainer ActiveContainer;

        // Current loose item that the bot will try to loot
        public LootItem ActiveItem;

        // Center of the container's collider used to help in navigation
        public Vector3 LootObjectCenter;

        // Where the bot will try to move to
        public Vector3 Destination;

        // Container ids that the bot has looted
        public string[] VisitedContainerIds = new string[] { };

        // Container ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public string[] NonNavigableContainerIds = new string[] { };

        // Amount of time in seconds to wait after looting a container before finding the next container
        public float WaitAfterLooting = 0f;

        // Amount of times a bot has tried to navigate to a single container
        public int NavigationAttempts = 0;

        // Amount of times a bot has not moved during the isCloseEnough check
        public int StuckCount = 0;

        // Amount of time to wait before clearning the nonNavigableContainerIds array
        public float ClearNonNavigableIdTimer = 0f;

        // Current distance from bot to container
        public float Dist;

        public BotLootData(LootFinder lootFinder)
        {
            LootFinder = lootFinder;
        }

        public bool HasActiveLootable()
        {
            return ActiveContainer != null || ActiveItem != null;
        }
    }

    public static class LootCache
    {
        public static Dictionary<int, BotLootData> BotDataCache =
            new Dictionary<int, BotLootData>();
        public static Dictionary<string, int> ActiveLootCache = new Dictionary<string, int>();

        private static readonly float TimeToLoot = 6f;

        public static void Reset() {
            foreach(KeyValuePair<int, BotLootData> data in BotDataCache) {
                data.Value.LootFinder.Destroy();
            }

            BotDataCache = new Dictionary<int, BotLootData>();
            ActiveLootCache = new Dictionary<string, int>();
        }

        public static void SetLootData(int botId, BotLootData lootData)
        {
            BotDataCache[botId] = lootData;

            if (lootData.ActiveContainer != null || lootData.ActiveItem != null)
            {
                string id =
                    lootData.ActiveContainer != null
                        ? lootData.ActiveContainer.Id
                        : lootData.ActiveItem.ItemOwner.RootItem.Id;
                CacheActiveLootId(id, botId);
            }
        }

        public static BotLootData GetLootData(int botId)
        {
            if (!BotDataCache.TryGetValue(botId, out BotLootData lootData))
            {
                // containerData = new BotContainerData();
                BotDataCache.Add(botId, lootData);
            }

            return lootData;
        }

        public static BotLootData UpdateNavigationAttempts(int botId)
        {
            BotLootData containerData = GetLootData(botId);
            containerData.NavigationAttempts++;
            SetLootData(botId, containerData);
            return containerData;
        }

        public static BotLootData UpdateStuckCount(int botId)
        {
            BotLootData containerData = GetLootData(botId);
            containerData.StuckCount++;
            SetLootData(botId, containerData);
            return containerData;
        }

        public static void AddNonNavigableLoot(int botId, string containerId)
        {
            BotLootData containerData = GetLootData(botId);
            containerData.NonNavigableContainerIds = containerData.NonNavigableContainerIds
                .Append(containerId)
                .ToArray();
            SetLootData(botId, containerData);
        }

        public static void AddVisitedLoot(int botId, string containerId)
        {
            BotLootData containerData = GetLootData(botId);
            containerData.VisitedContainerIds = containerData.VisitedContainerIds
                .Append(containerId)
                .ToArray();
            SetLootData(botId, containerData);
        }

        public static void CacheActiveLootId(string containerId, int botId)
        {
            ActiveLootCache[containerId] = botId;
        }

        public static bool IsLootInUse(string containerId)
        {
            return ActiveLootCache.TryGetValue(containerId, out int botId);
        }

        public static bool IsLootIgnored(int botId, string lootId)
        {
            BotLootData botData = GetLootData(botId);
            bool alreadyTried =
                botData.NonNavigableContainerIds.Contains(lootId)
                || botData.VisitedContainerIds.Contains(lootId);

            return alreadyTried || IsLootInUse(lootId);
        }

        public static void IncrementLootTimer(int botId, float time = -1f)
        {
            // Increment loot wait timer in BotContainerData
            BotLootData botContainerData = GetLootData(botId);
            float timer = time != -1f ? time : LootingBots.TimeToWaitBetweenLoot.Value + TimeToLoot;

            botContainerData.WaitAfterLooting =
                Time.time + timer;

            SetLootData(botId, botContainerData);
        }

        // Original function is method_4
        public static void Cleanup(BotOwner botOwner, string lootId)
        {
            try
            {
                BotLootData botLootData = GetLootData(botOwner.Id);
                botLootData.NavigationAttempts = 0;
                botLootData.ActiveContainer = null;
                botLootData.ActiveItem = null;
                botLootData.LootObjectCenter = new Vector3();
                botLootData.Dist = 0;
                botLootData.StuckCount = 0;
                ActiveLootCache.Remove(lootId);

                SetLootData(botOwner.Id, botLootData);
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e.StackTrace);
            }
        }

        public static void Destroy(int botId)
        {
            BotDataCache.TryGetValue(botId, out BotLootData botLootData);
            if (botLootData != null)
            {
                botLootData.LootFinder.Destroy();
                BotDataCache.Remove(botId);
            }
        }
    }
}
