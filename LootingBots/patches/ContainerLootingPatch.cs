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
            sphere.transform.localScale = new Vector3(size, size, size);

            return sphere;
        }
    }

    public class ContainerLooting
    {
        public void Enable()
        {
            try
            {
                new CleanCacheOnDeadPatch().Enable();
                new ReservePatrolContainerPatch().Enable();
                new FindNearestContainerPatch().Enable();
                new ContainerManualUpdatePatch().Enable();
                new ContainerUpdateCheckPatch().Enable();
            }
            catch (Exception e)
            {
                LootingBots.containerLog.logError(e.StackTrace);
            }
        }
    }

    public class CleanCacheOnDeadPatch : ModulePatch {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod("OnDead", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix(ref Player __instance) {
            LootingBots.containerLog.logDebug($"Bot {__instance.Id} is dead. Removing bot data from container cache.");
            ContainerCache.botDataCache.Remove(__instance.Id);
        }
    }

    public class ContainerUpdateCheckPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "UpdateCheck",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref GClass326 __instance,
            ref BotOwner ___botOwner_0,
            ref GClass264 ___gclass264_0,
            ref float ___float_5,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotContainerData botContainerData = ContainerCache.getContainerData(___botOwner_0.Id);

            // Check if we have looted an item and the wait timer has completed
            bool Boolean_0 = ___bool_1 && ___float_5 < Time.time;

            // If there is not an active container or there is a body saved, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
                || !botContainerData?.activeContainer
                || ___gclass264_0 != null
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

        // Original function is GClass326.method_2
        public static void checkContainerStatus(
            bool DoneLootingTimer, // Boolean_0
            ref BotOwner botOwner, // botOwner_0
            ref bool shallLoot, // bool_2
            ref bool hasLooted, // bool_1
            LootableContainer container
        )
        {
            // If we have just looted a container, and the wait timer is finished cleanup the container from the map
            if (DoneLootingTimer)
            {
                LootingBots.containerLog.logWarning(
                    $"Removing successfully looted container: {container.name} ({container.Id})"
                );
                ContainerCache.cleanup(ref botOwner, container, ref shallLoot, ref hasLooted);
                ContainerCache.addVistedContainer(botOwner.Id, container.Id);
                return;
            }

            // TODO: Not sure if needed. Remove container if bot navigates too far away during patrol
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
        public static float TimeToLoot = 8f;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "ManualUpdate",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref BotOwner ___botOwner_0,
            ref GClass264 ___gclass264_0,
            ref float ___float_0,
            ref float ___float_4,
            ref float ___float_1,
            ref float ___float_5,
            ref bool ___bool_0,
            ref bool ___bool_1,
            ref bool ___bool_2
        )
        {
            BotContainerData botContainerData = ContainerCache.getContainerData(___botOwner_0.Id);

            // If there is no active container or if there is a corpse, execute the original method
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
                || !botContainerData?.activeContainer
                || ___gclass264_0 != null
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
                    ref ___bool_1,
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
                    ___float_5 = TimeToLoot + Time.time;
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

            // Initiate move to container. Will return false if the bot is not able to navigate using a NavMesh
            bool canMove = tryMoveToContainer(
                ref ___botOwner_0,
                ref ___float_1,
                container,
                ref ___bool_1,
                ref ___bool_2
            );

            // If there is not a valid path to the container, ignore the container forever
            if (!canMove)
            {
                ContainerCache.cleanup(ref ___botOwner_0, container, ref ___bool_2, ref ___bool_1);
                ContainerCache.addNonNavigableContainer(___botOwner_0.Id, container.Id);
            }
            return false;
        }

        private static bool shouldInteractDoor(
            BotOwner botOwner,
            float dist,
            LootableContainer container
        )
        {
            BotContainerData botContainerData = ContainerCache.getContainerData(botOwner.Id);

            // Calculate change in distance and assume any change less than 1 means the bot hasnt moved.
            float changeInDist = Math.Abs(botContainerData.dist - dist);

            if (changeInDist < 1)
            {
                LootingBots.containerLog.logError(
                    $"(Stuck: {botContainerData.stuckCount}) Bot {botOwner.Id} has not moved {changeInDist}. Dist from container: {dist}"
                );

                // Check for door with 1f sphere. TODO: Change to Ray
                Collider[] array = Physics.OverlapSphere(
                    botOwner.Position,
                    1.3f,
                    LayerMask.GetMask(new string[] { "Interactive", }),
                    QueryTriggerInteraction.Collide
                );

                // Loop through colliders and find an interactable door. If one is found, try to interact and return out of the method.
                foreach (Collider collider in array)
                {
                    Door door = collider.gameObject.GetComponentInParent<Door>();

                    if (door?.DoorState == EDoorState.Shut)
                    {
                        LootingBots.containerLog.logDebug($"Bot {botOwner.Id} Opening door");
                        GClass2599 interactionResult = new GClass2599(EInteractionType.Open);
                        botOwner.SetTargetMoveSpeed(0f);
                        botOwner.GetPlayer.CurrentState.StartDoorInteraction(
                            door,
                            interactionResult,
                            null
                        );
                        return true;
                    }
                    else if (door?.DoorState == EDoorState.Open)
                    {
                        LootingBots.containerLog.logDebug($"Bot {botOwner.Id} Closing door");
                        GClass2599 interactionResult = new GClass2599(EInteractionType.Close);
                        botOwner.SetTargetMoveSpeed(0f);
                        botOwner.GetPlayer.CurrentState.StartDoorInteraction(
                            door,
                            interactionResult,
                            null
                        );
                        return true;
                    }
                }

                // Bot is stuck, update stuck count
                ContainerCache.updateStuckCount(botOwner.Id);
            }
            else
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                botContainerData.dist = dist;
                botContainerData.stuckCount = 0;
                ContainerCache.setContainerData(botOwner.Id, botContainerData);
            }

            return false;
        }

        // Original function GClass326.method_1
        private static bool isCloseEnough(
            ref float closeEnoughTimer, // float_0
            ref float containerDist, // float_4
            ref bool isCloseEnough, // bool_0
            ref bool hasLooted, // bool_1
            ref BotOwner botOwner, // botOwner_0
            LootableContainer container,
            out float dist
        )
        {
            BotContainerData botContainerData = ContainerCache.getContainerData(botOwner.Id);
            if (closeEnoughTimer < Time.time && container != null)
            {
                closeEnoughTimer = Time.time + 2f;
                Vector3 vector = botOwner.Position - container.transform.position;
                float y = vector.y;
                vector.y = 0f;
                dist = containerDist = vector.magnitude;
                isCloseEnough = (containerDist < 1.6f && Mathf.Abs(y) < 1.3f);

                // If the bot is not looting anything, check to see if the bot is stuck on a door and open it
                if (!hasLooted)
                {
                    bool canInteract = shouldInteractDoor(botOwner, dist, container);

                    if (canInteract)
                    {
                        // closeEnoughTimer = Time.time + 6f;
                        return isCloseEnough;
                    }
                }
            }

            dist = containerDist;
            return isCloseEnough;
        }

        // Orignal function is GClass326.method_10
        private static bool tryMoveToContainer(
            ref BotOwner botOwner, // botOwner_0
            ref float tryMoveTimer, // float_1
            LootableContainer container,
            ref bool hasLooted, // bool_1
            ref bool ShallLoot // bool_2
        )
        {
            try
            {
                botOwner.Steering.LookToMovingDirection();

                if (tryMoveTimer < Time.time)
                {
                    BotContainerData containerData = ContainerCache.updateNavigationAttempts(
                        botOwner.Id
                    );

                    // If the bot has not been stuck for more than 4 navigation checks, attempt to navigate to the container otherwise ignore the container forever
                    if (containerData.stuckCount <= 4)
                    {
                        tryMoveTimer = Time.time + 6f;

                        NavMeshHit navMeshAlignedPoint;
                        Vector3 center = containerData.containerCenter;

                        // Try to snap the desired destination point to the nearest NavMesh to ensure the bot can draw a navigable path to the point
                        Vector3 pointNearbyContainer = NavMesh.SamplePosition(
                            center,
                            out navMeshAlignedPoint,
                            1f,
                            NavMesh.AllAreas
                        )
                            ? navMeshAlignedPoint.position
                            : Vector3.zero;

                        // Since SamplePosition always snaps to the closest point on the NavMesh, sometimes this point is a little too close to the container and causes the bot to shake violently while looting.
                        // Add a small amount of padding by pushing the point away from the nearbyPoint
                        Vector3 padding = center - pointNearbyContainer;
                        padding.y = 0;
                        padding.Normalize();

                        // Make sure the point is still snapped to the NavMesh after its been pushed
                        pointNearbyContainer = NavMesh.SamplePosition(
                            center - padding,
                            out navMeshAlignedPoint,
                            1f,
                            navMeshAlignedPoint.mask
                        )
                            ? navMeshAlignedPoint.position
                            : pointNearbyContainer;

                        // Debug for bot container navigation
                        if (LootingBots.debugContainerNav.Value)
                        {
                            GameObjectHelper.drawSphere(
                                container.transform.position,
                                0.5f,
                                Color.red
                            );
                            GameObjectHelper.drawSphere(center - padding, 0.5f, Color.green);
                            if (pointNearbyContainer != Vector3.zero)
                            {
                                GameObjectHelper.drawSphere(pointNearbyContainer, 0.5f, Color.blue);
                            }
                            GameObjectHelper.drawSphere(center, 0.5f, Color.black);
                        }

                        // If we were able to snap the container position to a NavMesh, attempt to navigate
                        if (pointNearbyContainer != Vector3.zero)
                        {
                            NavMeshPathStatus pathStatus = botOwner.GoToPoint(
                                pointNearbyContainer,
                                true,
                                -1f,
                                false,
                                false,
                                true
                            );

                            LootingBots.containerLog.logDebug(
                                $"(Attempt: {containerData.navigationAttempts}) Bot {botOwner.Id} Moving to {container.ItemOwner.Items.ToArray()[0].Name.Localized()} status: {pathStatus}"
                            );

                            if (pathStatus != NavMeshPathStatus.PathComplete)
                            {
                                LootingBots.containerLog.logWarning(
                                    $"Bot {botOwner.Id} has no valid path for container: {container.name}. Ignoring"
                                );
                                return false;
                            }
                        }
                        else
                        {
                            LootingBots.containerLog.logWarning(
                                $"Bot {botOwner.Id} unable to snap container position to NavMesh. Ignoring container {container.name}"
                            );
                            return false;
                        }
                    }
                    else
                    {
                        LootingBots.containerLog.logWarning(
                            $"Bot {botOwner.Id} maximum navigation attempts exceeded for: {container.name}.Ignoring"
                        );
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            { 
                LootingBots.containerLog.logError(e.Message);
                LootingBots.containerLog.logError(e.StackTrace);
                return false;
            }
        }

        public static async void lootContainer(LootableContainer container, BotOwner botOwner)
        {
            ItemAdder itemAdder = new ItemAdder(botOwner);
            Item item = container.ItemOwner.Items.ToArray()[0];
            LootingBots.containerLog.logDebug($"Bot {botOwner.Id} trying to add items from: {item.Name.Localized()}");

            // Trigger open interaction on container
            botOwner.LootOpener.Interact(container, EInteractionType.Open);
            await itemAdder.lootNestedItems(item);
            
            // Close container and switch to main weapon
            botOwner.LootOpener.Interact(container, EInteractionType.Close);
            botOwner.WeaponManager.Selector.TakeMainWeapon();

            // Increment loot wait timer in BotContainerData
            BotContainerData botContainerData = ContainerCache.getContainerData(botOwner.Id);

            botContainerData.waitAfterLooting =
                Time.time + LootingBots.timeToWaitBetweenContainers.Value + TimeToLoot;

            ContainerCache.setContainerData(botOwner.Id, botContainerData);
        }
    }

    public class FindNearestContainerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass326).GetMethod(
                "method_3",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(
            ref GClass326 __instance,
            ref BotOwner ___botOwner_0,
            ref float ___float_2,
            ref GClass264 ___gclass264_0,
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

            BotContainerData botContainerData = ContainerCache.getContainerData(___botOwner_0.Id);

            // Only apply container detection if there is no active corpse and we are not in a delay between looting containers
            if (
                botContainerData.waitAfterLooting < Time.time
                && ___float_2 < Time.time
                && ___gclass264_0 == null
            )
            {
                // If we have an active container already do not scan
                if (botContainerData?.activeContainer)
                {
                    LootingBots.containerLog.logWarning(
                        $"Bot {___botOwner_0.Id} existing container: {botContainerData.activeContainer.name}"
                    );
                    // Set ShallLoot to true
                    ___bool_2 = true;
                    return;
                }

                LootableContainer closestContainer = null;
                Vector3 closesContainerCenter = new Vector3();
                float shortestDist = -1f;

                // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
                Collider[] array = Physics.OverlapSphere(
                    ___botOwner_0.Position,
                    LootingBots.detectContainerDistance.Value,
                    LayerMask.GetMask(new string[] { "Interactive", }),
                    QueryTriggerInteraction.Collide
                );

                // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
                foreach (Collider collider in array)
                {
                    LootableContainer containerObj =
                        collider.gameObject.GetComponentInParent<LootableContainer>();

                    if (
                        containerObj != null
                        && !ContainerCache.isContainerIgnored(___botOwner_0.Id, containerObj.Id)
                        && containerObj.isActiveAndEnabled
                        && containerObj.DoorState != EDoorState.Locked
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
                            closesContainerCenter = collider.bounds.center;
                            // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                            closesContainerCenter.y =
                                collider.bounds.center.y - collider.bounds.extents.y - 0.3f;
                        }
                    }
                }

                if (closestContainer != null)
                {
                    LootingBots.containerLog.logDebug(
                        $"Clostest container: {closestContainer.name.Localized()} ({closestContainer.Id})"
                    );
                    // Add closest container found to container map
                    botContainerData.activeContainer = closestContainer;
                    botContainerData.containerCenter = closesContainerCenter;

                    ContainerCache.setContainerData(___botOwner_0.Id, botContainerData);

                    // Set ShallLoot to true
                    ___bool_2 = true;
                }
            }
        }
    }
}
