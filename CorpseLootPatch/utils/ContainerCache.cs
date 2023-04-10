using System;
using System.Linq;
using EFT.Interactive;
using EFT;
using UnityEngine;
using System.Collections.Generic;

namespace LootingBots.Patch.Util
{
    public class BotContainerData
    {
        // Current container that the bot will try to loot
        public LootableContainer activeContainer;
        // Center of the container's collider used to help in navigation
        public Vector3 containerCenter;

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
    }

    public static class ContainerCache
    {
        public static Dictionary<int, BotContainerData> botDataCache =
            new Dictionary<int, BotContainerData>();
        public static Dictionary<string, int> activeContainerCache = new Dictionary<string, int>();

        public static void setContainerData(int botId, BotContainerData containerData)
        {
            botDataCache[botId] = containerData;

            if (containerData.activeContainer) {
              cacheActiveContainerId(containerData.activeContainer.Id, botId);
            }
        }

        public static BotContainerData getContainerData(int botId)
        {
            BotContainerData containerData;

            if (!botDataCache.TryGetValue(botId, out containerData))
            {
                containerData = new BotContainerData();
                botDataCache.Add(botId, containerData);
            }

            return containerData;
        }

        public static BotContainerData updateNavigationAttempts(int botId)
        {
            BotContainerData containerData = getContainerData(botId);
            containerData.navigationAttempts++;
            setContainerData(botId, containerData);
            return containerData;
        }

        public static BotContainerData updateStuckCount(int botId)
        {
            BotContainerData containerData = getContainerData(botId);
            containerData.stuckCount++;
            setContainerData(botId, containerData);
            return containerData;
        }

        public static void addNonNavigableContainer(int botId, string containerId)
        {
            BotContainerData containerData = getContainerData(botId);
            containerData.nonNavigableContainerIds = containerData.nonNavigableContainerIds
                .Append(containerId)
                .ToArray();
            setContainerData(botId, containerData);
        }

        public static void addVistedContainer(int botId, string containerId)
        {
            BotContainerData containerData = getContainerData(botId);
            containerData.visitedContainerIds = containerData.visitedContainerIds
                .Append(containerId)
                .ToArray();
            setContainerData(botId, containerData);
        }
        public static void cacheActiveContainerId(string containerId, int botId) {
          activeContainerCache[containerId] = botId;
        }

        public static bool isContainerInUse(string containerId) {
          int botId;
          return activeContainerCache.TryGetValue(containerId, out botId);
        }

        public static bool isContainerIgnored(int botId, string containerId)
        {
            BotContainerData botData = getContainerData(botId);
            bool alreadyTried =
                botData.nonNavigableContainerIds.Contains(containerId)
                || botData.visitedContainerIds.Contains(containerId);

            
            return alreadyTried || isContainerInUse(containerId);
        }

        // Original function is method_4
        public static void cleanup(
            ref BotOwner botOwner,
            LootableContainer container,
            ref bool ShallLoot,
            ref bool hasLooted
        )
        {
            try
            {
                BotContainerData botContainerData = getContainerData(botOwner.Id);
                botContainerData.navigationAttempts = 0;
                botContainerData.activeContainer = null;
                botContainerData.containerCenter = new Vector3();
                botContainerData.dist = 0;
                activeContainerCache.Remove(container.Id);

                setContainerData(botOwner.Id, botContainerData);

                ShallLoot = false;
                hasLooted = false;
            }
            catch (Exception e)
            {
                LootingBots.lootLog.logError(e.StackTrace);
            }
        }
    }
}
