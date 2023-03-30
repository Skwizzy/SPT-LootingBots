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
    public static class LootMap
    {
        public static Dictionary<int, LootableContainer> containerMap =
            new Dictionary<int, LootableContainer>();

        public static Dictionary<int, string[]> visitedContainerMap =
            new Dictionary<int, string[]>();

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

                string[] visited;
                LootMap.visitedContainerMap.TryGetValue(___botOwner_0.Id, out visited);

                if (visited != null)
                {
                    LootMap.visitedContainerMap[___botOwner_0.Id] = visited
                        .Append(container.Id)
                        .ToArray();
                }
                else
                {
                    visited = new string[] { container.Id };
                    LootMap.visitedContainerMap.Add(___botOwner_0.Id, visited);
                }

                LootMap.containerMap.Remove(___botOwner_0.Id);
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
                new LootContainerPatch1().Enable();
                new LootContainerPatch2().Enable();
                new LootContainerPatch3().Enable();
            }
            catch (Exception e)
            {
                LootingBots.log.logError(e.StackTrace);
            }
        }
    }

    public class LootContainerPatch3 : ModulePatch
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
            LootableContainer container;
            LootMap.containerMap.TryGetValue(___botOwner_0.Id, out container);

            // Check if we have looted an item and the wait timer has completed
            bool Boolean_0 = ___bool_1 && ___float_5 < Time.time;

            // If there is not an active container or there is a body saved, execute the original method
            if (!container || ___gclass263_0 != null)
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
                    container
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

    public class LootContainerPatch2 : ModulePatch
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
            LootableContainer container;
            LootMap.containerMap.TryGetValue(___botOwner_0.Id, out container);

            // If there is no active container or if there is a corpse, execute the original method
            if (!container || ___gclass263_0 != null)
            {
                return true;
            }

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

                NavMeshPath navMeshPath = new NavMeshPath();
                bool hasPath = NavMesh.CalculatePath(
                    botOwner_0.Position,
                    position,
                    -1,
                    navMeshPath
                );

                // Additional check on NavMeshPath before issuing GoToPoint
                if (hasPath)
                {
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
                        cleanup(ref botOwner_0, container, ref bool_2, ref bool_1);
                    }
                }
                else
                {
                    cleanup(ref botOwner_0, container, ref bool_2, ref bool_1);
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
        }

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
                Logger.LogWarning(
                    $"Removing container for bot {___botOwner_0.Profile?.Info.Nickname.TrimEnd()}"
                );

                string[] visited;
                LootMap.visitedContainerMap.TryGetValue(___botOwner_0.Id, out visited);

                // Add the removed container to the visited container map
                if (visited != null)
                {
                    LootMap.visitedContainerMap[___botOwner_0.Id] = visited
                        .Append(container.Id)
                        .ToArray();
                }
                else
                {
                    visited = new string[] { container.Id };
                    LootMap.visitedContainerMap.Add(___botOwner_0.Id, visited);
                }

                // Remove container from active container map
                LootMap.containerMap.Remove(___botOwner_0.Id);

                // Set ShallLoot and has looted item to false
                ___bool_2 = false;
                ___bool1 = false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
        }
    }

    public class LootContainerPatch1 : ModulePatch
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
            // Only apply container detection if there is no active corpse and if the bot is not a sniper bot
            if (
                ___float_2 < Time.time
                && ___gclass263_0 == null
                && ___botOwner_0.Profile.Info.Settings.Role != WildSpawnType.marksman
            )
            {
                LootableContainer existingContainer;
                if (LootMap.containerMap.TryGetValue(___botOwner_0.Id, out existingContainer))
                {
                    Logger.LogWarning(
                        $"Bot {___botOwner_0.Profile?.Info.Nickname.TrimEnd()} existing container: {existingContainer?.name}"
                    );
                    return;
                }

                string[] visitedContainers;
                LootMap.visitedContainerMap.TryGetValue(___botOwner_0.Id, out visitedContainers);

                LootableContainer closestContainer = null;
                float shortestDist = -1f;

                if (!existingContainer)
                {
                    // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
                    Collider[] array = Physics.OverlapSphere(
                        ___botOwner_0.Position,
                        25f,
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
                            Vector3 vector =
                                ___botOwner_0.Position - containerObj.transform.position;
                            float y = vector.y;
                            vector.y = 0f;
                            float dist = vector.magnitude;

                            Item container = containerObj.ItemOwner.Items.ToArray()[0];

                            if (shortestDist == -1f || dist < shortestDist)
                            {
                                shortestDist = dist;
                                closestContainer = containerObj;
                            }
                        }
                    }

                    if (closestContainer != null)
                    {
                        Logger.LogWarning(
                            $"Clostest container: {closestContainer.name.Localized()}"
                        );
                        Logger.LogWarning(
                            $"Last visited container count: {visitedContainers?.Length}"
                        );
                        // Add closest container found to container map
                        LootMap.containerMap.Add(___botOwner_0.Id, closestContainer);

                        // Set ShallLoot to true
                        ___bool_2 = true;
                    }
                }
            }
        }
    }
}
