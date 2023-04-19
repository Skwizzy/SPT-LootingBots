using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.AI;
using System;
using System.Linq;
using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class LootFinder : MonoBehaviour
    {
        public BotOwner botOwner;
        public ItemAdder itemAdder;
        public static float TimeToLoot = 8f;
        private static float scanTimer;

        public void Update()
        {
            // Check to see if the current bot has container looting enabled
            if (
                !LootingBots.dynamicContainerLootingEnabled.Value.isBotEnabled(
                    botOwner.Profile.Info.Settings.Role
                )
            )
            {
                return;
            }

            BotLootData botLootData = LootCache.getLootData(botOwner.Id);

            if (itemAdder == null && botOwner != null)
            {
                itemAdder = new ItemAdder(botOwner);
            }

            if (
                botLootData.waitAfterLooting < Time.time
                && scanTimer < Time.time
                && !botLootData.hasActiveLootable()
            )
            {
                findLootable();
            }
        }

        public void findLootable()
        {
            scanTimer = Time.time + 6f;
            BotLootData botLootData = LootCache.getLootData(botOwner.Id);

            LootableContainer closestContainer = null;
            LootItem closestItem = null;
            Vector3 closestLootableCenter = new Vector3();
            float shortestDist = -1f;

            // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                botOwner.Position,
                LootingBots.detectContainerDistance.Value,
                LayerMask.GetMask(new string[] { "Interactive", "Loot" }),
                QueryTriggerInteraction.Collide
            );

            // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
            foreach (Collider collider in array)
            {
                LootableContainer containerObj =
                    collider.gameObject.GetComponentInParent<LootableContainer>();
                LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();

                bool canLootContainer =
                    containerObj != null
                    && !LootCache.isLootIgnored(botOwner.Id, containerObj.Id)
                    && containerObj.isActiveAndEnabled
                    && containerObj.DoorState != EDoorState.Locked;

                bool canLootItem =
                    lootItem != null
                    && lootItem?.ItemOwner?.RootItem != null
                    && !LootCache.isLootIgnored(botOwner.Id, lootItem.ItemOwner.RootItem.Id);

                if (canLootContainer || canLootItem)
                {
                    // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                    Vector3 vector =
                        botOwner.Position
                        - (containerObj?.transform?.position ?? lootItem.transform.position);
                    float y = vector.y;
                    vector.y = 0f;
                    float dist = vector.magnitude;

                    // If we are considering a container to be the new closest container, make sure the bot has a valid NavMeshPath for the container before adding it as the closest container
                    if ((shortestDist == -1f || dist < shortestDist))
                    {
                        shortestDist = dist;

                        if (canLootContainer)
                        {
                            closestItem = null;
                            closestContainer = containerObj;
                        }
                        else
                        {
                            closestContainer = null;
                            closestItem = lootItem;
                        }

                        closestLootableCenter = collider.bounds.center;
                        // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                        closestLootableCenter.y =
                            collider.bounds.center.y - collider.bounds.extents.y - 0.4f;
                    }
                }
            }

            if (closestContainer != null)
            {
                LootingBots.containerLog.logDebug(
                    $"Clostest container: {closestContainer.name.Localized()} ({closestContainer.Id})"
                );
                // Add closest container found to container map
                botLootData.activeContainer = closestContainer;
                botLootData.lootObjectCenter = closestLootableCenter;

                LootCache.setLootData(botOwner.Id, botLootData);
            }
            else if (closestItem != null)
            {
                LootingBots.containerLog.logDebug(
                    $"Clostest item: {closestItem.name.Localized()} ({closestItem.ItemId})"
                );
                // Add closest container found to container map
                botLootData.activeItem = closestItem;
                botLootData.lootObjectCenter = closestLootableCenter;

                LootCache.setLootData(botOwner.Id, botLootData);
            }
        }

        public async void lootContainer(LootableContainer container)
        {
            // ItemAdder itemAdder = new ItemAdder(botOwner);
            Item item = container.ItemOwner.Items.ToArray()[0];
            LootingBots.containerLog.logDebug(
                $"Bot {botOwner.Id} trying to add items from: {item.Name.Localized()}"
            );

            // Trigger open interaction on container
            botOwner.LootOpener.Interact(container, EInteractionType.Open);
            await itemAdder.lootNestedItems(item);

            // Close container and switch to main weapon
            botOwner.WeaponManager.Selector.TakeMainWeapon();
            LootCache.incrementLootTimer(botOwner.Id);
        }

        public async void lootItem()
        {
            BotLootData botLootData = LootCache.getLootData(botOwner.Id);
            Item item = botLootData.activeItem.ItemOwner.RootItem;

            LootingBots.containerLog.logDebug(
                $"Bot {botOwner.Id} trying to pick up loose item: {item.Name.Localized()}"
            );
            botOwner.GetPlayer.UpdateInteractionCast();

            await itemAdder.tryAddItemsToBot(new Item[] { item });
            botOwner.GetPlayer.CurrentState.Pickup(false, null);

            LootCache.incrementLootTimer(botOwner.Id);
        }

        public bool shouldInteractDoor(BotOwner botOwner, float dist, LootableContainer container)
        {
            BotLootData botContainerData = LootCache.getLootData(botOwner.Id);

            // Calculate change in distance and assume any change less than 1 means the bot hasnt moved.
            float changeInDist = Math.Abs(botContainerData.dist - dist);

            if (changeInDist < 1)
            {
                LootingBots.containerLog.logDebug(
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
                LootCache.updateStuckCount(botOwner.Id);
            }
            else
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                botContainerData.dist = dist;
                botContainerData.stuckCount = 0;
                LootCache.setLootData(botOwner.Id, botContainerData);
            }

            return false;
        }

        public bool isCloseEnough(out float dist)
        {
            BotLootData lootData = LootCache.getLootData(botOwner.Id);
            Vector3 vector = botOwner.Position - lootData.destination;
            float y = vector.y;
            vector.y = 0f;
            dist = vector.magnitude;
            return dist < 0.5f;
        }

        public bool tryMoveToLoot(ref float tryMoveTimer)
        {
            try
            {
                botOwner.Steering.LookToMovingDirection();

                if (tryMoveTimer < Time.time)
                {
                    BotLootData botLootData = LootCache.updateNavigationAttempts(botOwner.Id);
                    string lootableName =
                        botLootData.activeContainer != null
                            ? botLootData.activeContainer.ItemOwner.Items.ToArray()[
                                0
                            ].Name.Localized()
                            : botLootData.activeItem.Name.Localized();

                    // If the bot has not been stuck for more than 4 navigation checks, attempt to navigate to the container otherwise ignore the container forever
                    if (botLootData.stuckCount <= 4)
                    {
                        tryMoveTimer = Time.time + 2f;

                        NavMeshHit navMeshAlignedPoint;
                        Vector3 center = botLootData.lootObjectCenter;

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
                        botLootData.destination = pointNearbyContainer = NavMesh.SamplePosition(
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
                            GameObjectHelper.drawSphere(center, 0.5f, Color.red);
                            GameObjectHelper.drawSphere(center - padding, 0.5f, Color.green);
                            if (pointNearbyContainer != Vector3.zero)
                            {
                                GameObjectHelper.drawSphere(pointNearbyContainer, 0.5f, Color.blue);
                            }
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
                                $"(Attempt: {botLootData.navigationAttempts}) Bot {botOwner.Id} moving to {lootableName} status: {pathStatus}"
                            );

                            if (pathStatus != NavMeshPathStatus.PathComplete)
                            {
                                LootingBots.containerLog.logWarning(
                                    $"Bot {botOwner.Id} has no valid path to: {lootableName}. Ignoring"
                                );
                                return false;
                            }

                            LootCache.setLootData(botOwner.Id, botLootData);
                        }
                        else
                        {
                            LootingBots.containerLog.logWarning(
                                $"Bot {botOwner.Id} unable to snap container position to NavMesh. Ignoring {lootableName}"
                            );
                            return false;
                        }
                    }
                    else
                    {
                        LootingBots.containerLog.logWarning(
                            $"Bot {botOwner.Id} maximum navigation attempts exceeded for: {lootableName}. Ignoring"
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
    }
}
