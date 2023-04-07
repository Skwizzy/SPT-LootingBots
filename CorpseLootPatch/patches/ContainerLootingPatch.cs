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
    // Degug spheres from DrakiaXYZ Waypoints https://github.com/DrakiaXYZ/SPT-Waypoints/blob/master/Helpers/GameObjectHelper.cs
    public class GameObjectHelper
    {
        public static GameObject drawSphere(Vector3 position, float size, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().material.color = color;
            sphere.GetComponent<Collider>().enabled = false;
            sphere.transform.position = new Vector3(position.x, position.y, position.z);
            ;
            sphere.transform.localScale = new Vector3(size, size, size);

            return sphere;
        }
    }

    public class BotContainerData
    {
        // Current container that the bot will try to loot
        public LootableContainer activeContainer;

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

        public float dist;
    }

    public static class ContainerDataMap
    {
        public static Dictionary<int, BotContainerData> containerDataMap =
            new Dictionary<int, BotContainerData>();

        public static void setContainerData(int botId, BotContainerData containerData)
        {
            containerDataMap[botId] = containerData;
        }

        public static BotContainerData getContainerData(int botId)
        {
            BotContainerData containerData;

            if (!containerDataMap.TryGetValue(botId, out containerData))
            {
                containerData = new BotContainerData();
                containerDataMap.Add(botId, containerData);
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

        public static BotContainerData refreshNonNavigableContainers(int botId)
        {
            BotContainerData containerData = getContainerData(botId);
            // Clear non navigable containers every 5 minutes to allow bots to try and see if there is a new valid path
            if (containerData.clearNonNavigableIdTimer < Time.time)
            {
                LootingBots.log.logDebug("Clearing saved non-navigable containers");

                containerData.nonNavigableContainerIds = new string[] { };
                ContainerDataMap.setContainerData(botId, containerData);

                containerData.clearNonNavigableIdTimer = Time.time + 300f;
            }

            return containerData;
        }

        public static bool isContainerIgnored(int botId, string containerId)
        {
            BotContainerData botData = getContainerData(botId);
            return botData.nonNavigableContainerIds.Contains(containerId)
                || botData.visitedContainerIds.Contains(containerId);
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
                BotContainerData botContainerData = getContainerData(___botOwner_0.Id);
                botContainerData.navigationAttempts = 0;
                botContainerData.activeContainer = null;
                botContainerData.dist = 0;

                setContainerData(___botOwner_0.Id, botContainerData);

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
            BotContainerData botContainerData = ContainerDataMap.getContainerData(___botOwner_0.Id);

            // Check if we have looted an item and the wait timer has completed
            bool Boolean_0 = ___bool_1 && ___float_5 < Time.time;

            // If there is not an active container or there is a body saved, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
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
                LootingBots.log.logWarning(
                    $"Removing successfully looted container: {container.name} ({container.Id})"
                );
                ContainerDataMap.cleanup(ref ___botOwner_0, container, ref bool_2, ref bool_1);
                ContainerDataMap.addVistedContainer(___botOwner_0.Id, container.Id);
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
            BotContainerData botContainerData = ContainerDataMap.getContainerData(___botOwner_0.Id);

            // If there is no active container or if there is a corpse, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
                || !botContainerData?.activeContainer
                || ___gclass263_0 != null
            )
            {
                return true;
            }

            LootableContainer container = botContainerData.activeContainer;
            float dist;
            if (
                isCloseEnough(
                    ref ___float_0,
                    ref ___float_4,
                    ref ___bool_0,
                    ___bool_1,
                    ref ___botOwner_0,
                    container,
                    out dist
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
            bool bool_1,
            ref BotOwner botOwner_0,
            LootableContainer container,
            out float dist
        )
        {
            BotContainerData botContainerData = ContainerDataMap.getContainerData(botOwner_0.Id);
            if (float_0 < Time.time && container != null)
            {
                float_0 = Time.time + 2f;
                Vector3 vector = botOwner_0.Position - container.transform.position;
                float y = vector.y;
                vector.y = 0f;
                dist = float_4 = vector.magnitude;
                bool_0 = (float_4 < 1.5f && Mathf.Abs(y) < 1.3f);
                float changeInDist = Math.Abs(botContainerData.dist - dist);

                if (changeInDist < 1 && !bool_1)
                {
                    // Quick and dirty door check
                    LootingBots.log.logError(
                        $"Bot {botOwner_0.Id} has not moved {changeInDist}. Container position: {container.transform.position.ToJson()}"
                    );
                    Collider[] array = Physics.OverlapSphere(
                        botOwner_0.Position,
                        1f,
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

                    foreach (Collider collider in array)
                    {
                        LootingBots.log.logDebug(collider.gameObject);
                        Door door = collider.gameObject.GetComponentInParent<Door>();
                        if (door != null && door.DoorState == EDoorState.Shut)
                        {
                            LootingBots.log.logError("Found door");
                            botOwner_0.DoorOpener.Interact(door, EInteractionType.Open);
                            float_0 = Time.time + 6f;
                            return bool_0;
                        }
                    }

                    ContainerDataMap.updateStuckCount(botOwner_0.Id);
                }
                else
                {
                    botContainerData.dist = dist;
                    botContainerData.stuckCount = 0;
                    ContainerDataMap.setContainerData(botOwner_0.Id, botContainerData);
                }

                return bool_0;
            }

            dist = float_4;
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
                BotContainerData containerData = ContainerDataMap.updateNavigationAttempts(
                    botOwner_0.Id
                );

                if (containerData.stuckCount <= 4)
                {
                    float_1 = Time.time + 8f;
                    Vector3 position = container.transform.position;
                    Vector3 vector = GClass780.NormalizeFastSelf(position - botOwner_0.Position);
                    Vector3 pointNearbyContainer = position - vector;

                    NavMeshHit myNavHit;

                    if (
                        NavMesh.SamplePosition(
                            pointNearbyContainer,
                            out myNavHit,
                            1,
                            NavMesh.AllAreas
                        )
                    )
                    {
                        pointNearbyContainer = myNavHit.position;
                    }

                    // Debug
                    // GameObjectHelper.drawSphere(position, 0.5f, Color.red);
                    // GameObjectHelper.drawSphere(position - vector, 0.5f, Color.green);
                    // GameObjectHelper.drawSphere(pointNearbyContainer, 0.5f, Color.blue);

                    NavMeshPathStatus pathStatus = botOwner_0.GoToPoint(
                        pointNearbyContainer,
                        true,
                        -1f,
                        false,
                        false,
                        true
                    );

                    LootingBots.log.logDebug(
                        $"(Attempt: {containerData.navigationAttempts}) Bot {botOwner_0.Id} Moving to {container.ItemOwner.Items.ToArray()[0].Name.Localized()} status: {pathStatus}"
                    );

                    if (pathStatus != NavMeshPathStatus.PathComplete)
                    {
                        LootingBots.log.logWarning(
                            $"No valid path for container: {container.name}. Temporarily ignored"
                        );
                        ContainerDataMap.cleanup(ref botOwner_0, container, ref bool_2, ref bool_1);
                        ContainerDataMap.addNonNavigableContainer(botOwner_0.Id, container.Id);
                    }
                }
                else
                {
                    LootingBots.log.logWarning(
                        $"Maximum navigation attempts exceeded for: {container.name}. Temporarily ignored"
                    );
                    ContainerDataMap.cleanup(ref botOwner_0, container, ref bool_2, ref bool_1);
                    ContainerDataMap.addNonNavigableContainer(botOwner_0.Id, container.Id);
                }
            }
        }

        public static async void lootContainer(LootableContainer container, BotOwner ___botOwner_0)
        {
            ItemAdder itemAdder = new ItemAdder(___botOwner_0);
            Item item = container.ItemOwner.Items.ToArray()[0];
            LootingBots.log.logDebug($"Trying to add items from: {item.Name.Localized()}");

            await itemAdder.lootNestedItems(item);
            ___botOwner_0.WeaponManager.Selector.TakeMainWeapon();

            // Increment loot wait timer in BotContainerData
            BotContainerData botContainerData = ContainerDataMap.getContainerData(___botOwner_0.Id);

            botContainerData.waitAfterLooting =
                Time.time + LootingBots.timeToWaitBetweenContainers.Value;

            ContainerDataMap.setContainerData(___botOwner_0.Id, botContainerData);
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
            // Check to see if the current bot has container looting enabled
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
            )
            {
                return;
            }

            BotContainerData botContainerData = ContainerDataMap.getContainerData(___botOwner_0.Id);

            // Only apply container detection if there is no active corpse and we are not in a delay between looting containers
            if (
                botContainerData.waitAfterLooting < Time.time
                && ___float_2 < Time.time
                && ___gclass263_0 == null
            )
            {
                // If we have an active container already do not scan
                if (botContainerData?.activeContainer)
                {
                    LootingBots.log.logWarning(
                        $"Bot {___botOwner_0.Id} existing container: {botContainerData.activeContainer.name}"
                    );
                    // Set ShallLoot to true
                    ___bool_2 = true;
                    return;
                }

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
                        && !ContainerDataMap.isContainerIgnored(___botOwner_0.Id, containerObj.Id)
                    )
                    {
                        // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                        Vector3 vector = ___botOwner_0.Position - containerObj.transform.position;
                        float y = vector.y;
                        vector.y = 0f;
                        float dist = vector.magnitude;

                        Item container = containerObj.ItemOwner.Items.ToArray()[0];

                        // If we are considering a container to be the new closest container, make sure the bot has a valid NavMeshPath for the container before adding it as the closest container
                        if ((shortestDist == -1f || dist < shortestDist))
                        {
                            shortestDist = dist;
                            closestContainer = containerObj;
                        }
                    }
                }

                if (closestContainer != null)
                {
                    LootingBots.log.logDebug(
                        $"Clostest container: {closestContainer.name.Localized()} ({closestContainer.Id})"
                    );
                    // Add closest container found to container map
                    botContainerData.activeContainer = closestContainer;

                    ContainerDataMap.setContainerData(___botOwner_0.Id, botContainerData);

                    // Set ShallLoot to true
                    ___bool_2 = true;
                }
            }
        }
    }
}
