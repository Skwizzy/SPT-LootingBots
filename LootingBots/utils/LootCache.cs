using System;
using System.Linq;
using EFT.Interactive;
using EFT;
using UnityEngine;
using System.Collections.Generic;
using LootingBots.Patch.Components;

namespace LootingBots.Patch.Util
{
    public class BotLootData
    {
        public LootFinder lootFinder;

        // Current container that the bot will try to loot
        public LootableContainer activeContainer;

        // Current loose item that the bot will try to loot
        public LootItem activeItem;

        // Center of the container's collider used to help in navigation
        public Vector3 lootObjectCenter;

        // Where the bot will try to move to
        public Vector3 destination;

        // Container ids that the bot has looted
        public string[] visitedContainerIds = new string[] { };

        // Container ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public string[] nonNavigableContainerIds = new string[] { };

        // Amount of time in seconds to wait after looting a container before finding the next container
        public float waitAfterLooting = 0f;

        // Amount of times a bot has tried to navigate to a single container
        public int navigationAttempts = 0;

        // Amount of times a bot has not moved during the isCloseEnough check
        public int stuckCount = 0;

        // Amount of time to wait before clearning the nonNavigableContainerIds array
        public float clearNonNavigableIdTimer = 0f;

        // Current distance from bot to container
        public float dist;

        public BotLootData(LootFinder lootFinder)
        {
            this.lootFinder = lootFinder;
        }

        public bool hasActiveLootable()
        {
            return activeContainer != null || activeItem != null;
        }
    }

    public static class LootCache
    {
        public static Dictionary<int, BotLootData> botDataCache =
            new Dictionary<int, BotLootData>();
        public static Dictionary<string, int> activeLootCache = new Dictionary<string, int>();

        private static float TimeToLoot = 6f;

        public static void setLootData(int botId, BotLootData lootData)
        {
            botDataCache[botId] = lootData;

            if (lootData.activeContainer != null || lootData.activeItem != null)
            {
                string id =
                    lootData.activeContainer != null
                        ? lootData.activeContainer.Id
                        : lootData.activeItem.ItemOwner.RootItem.Id;
                cacheActiveLootId(id, botId);
            }
        }

        public static BotLootData getLootData(int botId)
        {
            BotLootData lootData;

            if (!botDataCache.TryGetValue(botId, out lootData))
            {
                // containerData = new BotContainerData();
                botDataCache.Add(botId, lootData);
            }

            return lootData;
        }

        public static BotLootData updateNavigationAttempts(int botId)
        {
            BotLootData containerData = getLootData(botId);
            containerData.navigationAttempts++;
            setLootData(botId, containerData);
            return containerData;
        }

        public static BotLootData updateStuckCount(int botId)
        {
            BotLootData containerData = getLootData(botId);
            containerData.stuckCount++;
            setLootData(botId, containerData);
            return containerData;
        }

        public static void addNonNavigableLoot(int botId, string containerId)
        {
            BotLootData containerData = getLootData(botId);
            containerData.nonNavigableContainerIds = containerData.nonNavigableContainerIds
                .Append(containerId)
                .ToArray();
            setLootData(botId, containerData);
        }

        public static void addVisitedLoot(int botId, string containerId)
        {
            BotLootData containerData = getLootData(botId);
            containerData.visitedContainerIds = containerData.visitedContainerIds
                .Append(containerId)
                .ToArray();
            setLootData(botId, containerData);
        }

        public static void cacheActiveLootId(string containerId, int botId)
        {
            activeLootCache[containerId] = botId;
        }

        public static bool isLootInUse(string containerId)
        {
            int botId;
            return activeLootCache.TryGetValue(containerId, out botId);
        }

        public static bool isLootIgnored(int botId, string containerId)
        {
            BotLootData botData = getLootData(botId);
            bool alreadyTried =
                botData.nonNavigableContainerIds.Contains(containerId)
                || botData.visitedContainerIds.Contains(containerId);

            return alreadyTried || isLootInUse(containerId);
        }

        public static void incrementLootTimer(int botId)
        {
            // Increment loot wait timer in BotContainerData
            BotLootData botContainerData = LootCache.getLootData(botId);

            botContainerData.waitAfterLooting =
                Time.time + LootingBots.timeToWaitBetweenContainers.Value + TimeToLoot;

            LootCache.setLootData(botId, botContainerData);
        }

        // Original function is method_4
        public static void cleanup(ref BotOwner botOwner, string lootId)
        {
            try
            {
                BotLootData botLootData = getLootData(botOwner.Id);
                botLootData.navigationAttempts = 0;
                botLootData.activeContainer = null;
                botLootData.activeItem = null;
                botLootData.lootObjectCenter = new Vector3();
                botLootData.dist = 0;
                botLootData.stuckCount = 0;
                activeLootCache.Remove(lootId);

                setLootData(botOwner.Id, botLootData);
            }
            catch (Exception e)
            {
                LootingBots.lootLog.logError(e.StackTrace);
            }
        }

        public static void destroy(int botId)
        {
            BotLootData botLootData = getLootData(botId);
            botLootData.lootFinder.destroy();
            LootCache.botDataCache.Remove(botId);
        }
    }
}
