using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using EFT.Interactive;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace LootingBots.Patch
{
    public class BotContainerData
    {
        public LootableContainer activeContainer;
        public string[] visitedContainerIds = new string[] { };
        public float waitAfterLooting = 0f;
    }

    public static class LootMap
    {
        public static Dictionary<int, BotContainerData> containerDataMap =
            new Dictionary<int, BotContainerData>();

        // Original function is method_4
        public static void cleanup(
            ref BotOwner ___botOwner_0,
            LootableContainer container,
            ref bool ___bool_2,
            ref bool ___bool1
        )
        {
            try
            {
                LootingBots.log.logWarning(
                    $"Removing container for bot {___botOwner_0.Profile?.Info.Nickname.TrimEnd()}"
                );

                BotContainerData botContainerData;
                LootMap.containerDataMap.TryGetValue(___botOwner_0.Id, out botContainerData);

                botContainerData.visitedContainerIds = botContainerData.visitedContainerIds
                    .Append(container.Id)
                    .ToArray();

                botContainerData.activeContainer = null;

                LootMap.containerDataMap[___botOwner_0.Id] = botContainerData;

                ___bool_2 = false;
                ___bool1 = false;
            }
            catch (Exception e)
            {
                LootingBots.log.logError(e.StackTrace);
            }
        }
    }

    public class ContainerLooting
    {
        public void Enable()
        {
            try
            {
                new ReservePatrolContainerPatch().Enable();
                new FindNearestContainerPatch().Enable();
                new ContainerManualUpdatePatch().Enable();
                new ContainerUpdateCheckPatch().Enable();
            }
            catch (Exception e)
            {
                LootingBots.log.logError(e.StackTrace);
            }
        }
    }

    public class ContainerUpdateCheckPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass325).GetMethod(
                "UpdateCheck",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref GClass325 __instance,
            ref BotOwner ___botOwner_0,
            ref GClass263 ___gclass263_0,
            ref float ___float_5,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotContainerData botContainerData;
            LootMap.containerDataMap.TryGetValue(___botOwner_0.Id, out botContainerData);

            // Check if we have looted an item and the wait timer has completed
            bool Boolean_0 = ___bool_1 && ___float_5 < Time.time;

            // If there is not an active container or there is a body saved, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value
                || !botContainerData?.activeContainer
                || ___gclass263_0 != null
            )
            {
                return true;
            }

            // If we have a container to loot, check to see if it no longer meets the criteria to loot
            if (___bool_2)
            {
                checkContainerStatus(
                    Boolean_0,
                    ref ___botOwner_0,
                    ref ___bool_2,
                    ref ___bool_1,
                    botContainerData.activeContainer
                );
                return false;
            }
            return true;
        }

        // Original function is GClass325.method_2
        public static void checkContainerStatus(
            bool Boolean_0,
            ref BotOwner ___botOwner_0,
            ref bool bool_2,
            ref bool bool_1,
            LootableContainer container
        )
        {
            // If we have just looted a container, and the wait timer is finished cleanup the container from the map
            if (Boolean_0)
            {
                Logger.LogError("Removing a container");
                LootMap.cleanup(ref ___botOwner_0, container, ref bool_2, ref bool_1);
                return;
            }

            // TODO: Remove container if bot navigates too far away during patrol
            // if (float_3 < Time.time)
            // {
            // 	float_3 = Time.time + 3f;
            // 	float num;
            // 	LootMap.method_1(ref ___float_0, ref ___float4, ref ___bool_0, ref ___botOwner_0,container);
            // 	if (num > botOwner_0.Settings.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST)
            // 	{
            // 		method_4();
            // 	}
            // }
        }
    }

    public class ContainerManualUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass325).GetMethod(
                "ManualUpdate",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref BotOwner ___botOwner_0,
            ref GClass263 ___gclass263_0,
            ref float ___float_0,
            ref float ___float_4,
            ref float ___float_1,
            ref float ___float_5,
            ref bool ___bool_0,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotContainerData botContainerData;
            LootMap.containerDataMap.TryGetValue(___botOwner_0.Id, out botContainerData);

            // If there is no active container or if there is a corpse, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value
                || !botContainerData?.activeContainer
                || ___gclass263_0 != null
            )
            {
                return true;
            }

            LootableContainer container = botContainerData.activeContainer;

            if (
                isCloseEnough(
                    ref ___float_0,
                    ref ___float_4,
                    ref ___bool_0,
                    ref ___botOwner_0,
                    container
                )
            )
            {
                // If the bot has not just looted something, loot the current container since we are now close enough
                if (!___bool_1)
                {
                    lootContainer(container, ___botOwner_0);
                    float num = 5f * 2f + 8f;
                    ___float_5 = num + Time.time;
                    ___bool_1 = true;
                }

                // Crouch and look to container
                ___botOwner_0.SetPose(0f);
                ___botOwner_0.Steering.LookToPoint(container.transform.position);

                return false;
            }

            // Stand and move to container
            ___botOwner_0.SetPose(1f);
            ___botOwner_0.SetTargetMoveSpeed(1f);
            tryMoveToContainer(
                ref ___botOwner_0,
                ref ___float_1,
                container,
                ref ___bool_1,
                ref ___bool_2
            );
            return false;
        }

        // Original function GClass325.method_1
        private static bool isCloseEnough(
            ref float float_0,
            ref float float_4,
            ref bool bool_0,
            ref BotOwner botOwner_0,
            LootableContainer container
        )
        {
            if (float_0 < Time.time && container != null)
            {
                float_0 = Time.time + 2f;
                Vector3 vector = botOwner_0.Position - container.transform.position;
                float y = vector.y;
                vector.y = 0f;
                float_4 = vector.magnitude;
                bool_0 = (float_4 < 1.4f && Mathf.Abs(y) < 1.3f);
                return bool_0;
            }

            return bool_0;
        }

        // Orignal function is GClass325.method_10
        private static void tryMoveToContainer(
            ref BotOwner botOwner_0,
            ref float float_1,
            LootableContainer container,
            ref bool bool_1,
            ref bool bool_2
        )
        {
            botOwner_0.Steering.LookToMovingDirection();

            if (float_1 < Time.time)
            {
                float_1 = Time.time + 6f;
                Vector3 position = container.transform.position;
                Vector3 vector = GClass780.NormalizeFastSelf(position - botOwner_0.Position);
                Vector3 position2 = position - vector;

                NavMeshPathStatus pathStatus = botOwner_0.GoToPoint(
                    position2,
                    true,
                    -1f,
                    false,
                    true,
                    true
                );

                Logger.LogInfo(
                    $"Bot {botOwner_0.Profile?.Info.Nickname.TrimEnd()} Moving to {container.ItemOwner.Items.ToArray()[0].Name.Localized()} status: {pathStatus}"
                );

                if (pathStatus == NavMeshPathStatus.PathInvalid || pathStatus == null)
                {
                    LootMap.cleanup(ref botOwner_0, container, ref bool_2, ref bool_1);
                }
            }
        }

        public static async void lootContainer(LootableContainer container, BotOwner ___botOwner_0)
        {
            ItemAdder itemAdder = new ItemAdder(___botOwner_0);
            Item item = container.ItemOwner.Items.ToArray()[0];
            Logger.LogDebug("Trying to add items");

            await itemAdder.lootNestedItems(item);
            ___botOwner_0.WeaponManager.Selector.TakeMainWeapon();

            // Increment loot wait timer in BotContainerData
            BotContainerData botContainerData;
            LootMap.containerDataMap.TryGetValue(___botOwner_0.Id, out botContainerData);
            botContainerData.waitAfterLooting =
                Time.time + LootingBots.timeToWaitBetweenContainers.Value;
            LootMap.containerDataMap[___botOwner_0.Id] = botContainerData;
        }
    }

    public class FindNearestContainerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass325).GetMethod(
                "method_3",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(
            ref GClass325 __instance,
            ref BotOwner ___botOwner_0,
            ref float ___float_2,
            ref GClass263 ___gclass263_0,
            ref bool ___bool_2
        )
        {
            if (!LootingBots.dynamicContainerLootingEnabled.Value)
            {
                return;
            }

            BotContainerData botContainerData;

            if (!LootMap.containerDataMap.TryGetValue(___botOwner_0.Id, out botContainerData))
            {
                botContainerData = new BotContainerData();
                LootMap.containerDataMap.Add(___botOwner_0.Id, botContainerData);
            }

            // Only apply container detection if there is no active corpse and if the bot is not a sniper bot
            if (
                botContainerData.waitAfterLooting < Time.time
                && ___gclass263_0 == null
                && ___botOwner_0.Profile.Info.Settings.Role != WildSpawnType.marksman
            )
            {
                // If we have an active container already do not scan
                if (botContainerData?.activeContainer)
                {
                    Logger.LogWarning(
                        $"Bot {___botOwner_0.Profile?.Info.Nickname.TrimEnd()} existing container: {botContainerData.activeContainer.name}"
                    );
                    return;
                }

                string[] visitedContainers = botContainerData.visitedContainerIds;
                LootableContainer closestContainer = null;
                float shortestDist = -1f;

                // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
                Collider[] array = Physics.OverlapSphere(
                    ___botOwner_0.Position,
                    LootingBots.detectContainerDistance.Value,
                    LayerMask.GetMask(
                        new string[]
                        {
                            "Interactive",
                            // "Deadbody",
                            // "Loot"
                        }
                    ),
                    QueryTriggerInteraction.Collide
                );

                // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
                foreach (Collider collider in array)
                {
                    LootableContainer containerObj =
                        collider.gameObject.GetComponentInParent<LootableContainer>();

                    if (
                        containerObj != null
                        && (
                            visitedContainers == null
                            || !visitedContainers.Contains(containerObj.Id)
                        )
                    )
                    {
                        // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                        Vector3 vector = ___botOwner_0.Position - containerObj.transform.position;
                        float y = vector.y;
                        vector.y = 0f;
                        float dist = vector.magnitude;

                        Item container = containerObj.ItemOwner.Items.ToArray()[0];

                        // If we are considering a container to be the new closest container, make sure the bot has a valid NavMeshPath for the container before adding it as the closest container
                        NavMeshPath navMeshPath = new NavMeshPath();
                        if (
                            (shortestDist == -1f || dist < shortestDist)
                            && NavMesh.CalculatePath(
                                ___botOwner_0.Position,
                                containerObj.transform.position,
                                -1,
                                navMeshPath
                            )
                        )
                        {
                            shortestDist = dist;
                            closestContainer = containerObj;
                        }
                    }
                }

                if (closestContainer != null)
                {
                    Logger.LogWarning($"Clostest container: {closestContainer.name.Localized()}");
                    Logger.LogWarning($"Last visited container count: {visitedContainers?.Length}");
                    // Add closest container found to container map
                    botContainerData.activeContainer = closestContainer;
                    LootMap.containerDataMap[___botOwner_0.Id] = botContainerData;

                    // Set ShallLoot to true
                    ___bool_2 = true;
                }
            }
        }
    }
}
