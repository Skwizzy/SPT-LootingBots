using System;
using System.Linq;

using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

using UnityEngine;
using UnityEngine.AI;

namespace LootingBots.Patch.Components
{
    public class LootFinder : MonoBehaviour
    {
        public BotOwner BotOwner;
        public ItemAdder ItemAdder;
        public static float TimeToLoot = 8f;
        private float _scanTimer;

        public void Update()
        {
            // Check to see if the current bot has container looting enabled
            if (
                !LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                    BotOwner.Profile.Info.Settings.Role
                )
                && !LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                    BotOwner.Profile.Info.Settings.Role
                )
            )
            {
                return;
            }

            BotLootData botLootData = LootCache.GetLootData(BotOwner.Id);

            if (ItemAdder == null && BotOwner != null)
            {
                ItemAdder = new ItemAdder(BotOwner);
            }

            if (
                botLootData.WaitAfterLooting < Time.time
                && _scanTimer < Time.time
                && !botLootData.HasActiveLootable()
            )
            {
                FindLootable();
            }
        }

        public void FindLootable()
        {
            _scanTimer = Time.time + 6f;
            BotLootData botLootData = LootCache.GetLootData(BotOwner.Id);

            LootableContainer closestContainer = null;
            LootItem closestItem = null;
            Vector3 closestLootableCenter = new Vector3();
            float shortestDist = -1f;

            // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                BotOwner.Position,
                LootingBots.DetectLootDistance.Value,
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
                    && !LootCache.IsLootIgnored(BotOwner.Id, containerObj.Id)
                    && containerObj.isActiveAndEnabled
                    && containerObj.DoorState != EDoorState.Locked
                    && LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    );

                bool canLootItem =
                    lootItem != null
                    && lootItem?.ItemOwner?.RootItem != null
                    && !LootCache.IsLootIgnored(BotOwner.Id, lootItem.ItemOwner.RootItem.Id)
                    && LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    );

                if (canLootContainer || canLootItem)
                {
                    // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                    Vector3 vector =
                        BotOwner.Position
                        - (containerObj?.transform?.position ?? lootItem.transform.position);
                    float y = vector.y;
                    vector.y = 0f;
                    float dist = vector.magnitude;

                    // If we are considering a container to be the new closest container, make sure the bot has a valid NavMeshPath for the container before adding it as the closest container
                    if (shortestDist == -1f || dist < shortestDist)
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
                LootingBots.LootLog.LogDebug(
                    $"Clostest container: {closestContainer.name.Localized()} ({closestContainer.Id})"
                );
                // Add closest container found to container map
                botLootData.ActiveContainer = closestContainer;
                botLootData.LootObjectCenter = closestLootableCenter;

                LootCache.SetLootData(BotOwner.Id, botLootData);
            }
            else if (closestItem != null)
            {
                LootingBots.LootLog.LogDebug(
                    $"Clostest item: {closestItem.name.Localized()} ({closestItem.ItemId})"
                );
                // Add closest container found to container map
                botLootData.ActiveItem = closestItem;
                botLootData.LootObjectCenter = closestLootableCenter;

                LootCache.SetLootData(BotOwner.Id, botLootData);
            }
        }

        public async void LootContainer(LootableContainer container)
        {
            // ItemAdder itemAdder = new ItemAdder(botOwner);
            Item item = container.ItemOwner.Items.ToArray()[0];
            LootingBots.LootLog.LogDebug(
                $"Bot {BotOwner.Id} trying to add items from: {item.Name.Localized()}"
            );

            // Trigger open interaction on container
            BotOwner.LootOpener.Interact(container, EInteractionType.Open);
            await ItemAdder.LootNestedItems(item);
            BotOwner.GetPlayer.UpdateInteractionCast();

            // // Close container and switch to main weapon
            BotOwner.WeaponManager.Selector.TakeMainWeapon();
            LootCache.IncrementLootTimer(BotOwner.Id);
        }

        public async void LootItem()
        {
            BotLootData botLootData = LootCache.GetLootData(BotOwner.Id);
            Item item = botLootData.ActiveItem.ItemOwner.RootItem;

            LootingBots.LootLog.LogDebug(
                $"Bot {BotOwner.Id} trying to pick up loose item: {item.Name.Localized()}"
            );
            BotOwner.GetPlayer.UpdateInteractionCast();

            await ItemAdder.TryAddItemsToBot(new Item[] { item });
            BotOwner.GetPlayer.CurrentState.Pickup(false, null);

            LootCache.IncrementLootTimer(BotOwner.Id);
            LootCache.Cleanup(ref BotOwner, item.Id);
            LootCache.AddVisitedLoot(BotOwner.Id, item.Id);
        }

        public bool ShouldInteractDoor(float dist)
        {
            BotLootData botContainerData = LootCache.GetLootData(BotOwner.Id);

            // Calculate change in distance and assume any change less than 1 means the bot hasnt moved.
            float changeInDist = Math.Abs(botContainerData.Dist - dist);

            if (changeInDist < 1)
            {
                LootingBots.LootLog.LogDebug(
                    $"(Stuck: {botContainerData.StuckCount}) Bot {BotOwner.Id} has not moved {changeInDist}. Dist from container: {dist}"
                );

                // Check for door with 1f sphere. TODO: Change to Ray
                Collider[] array = Physics.OverlapSphere(
                    BotOwner.Position,
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
                        LootingBots.LootLog.LogDebug($"Bot {BotOwner.Id} Opening door");
                        GClass2599 interactionResult = new GClass2599(EInteractionType.Open);
                        BotOwner.SetTargetMoveSpeed(0f);
                        BotOwner.GetPlayer.CurrentState.StartDoorInteraction(
                            door,
                            interactionResult,
                            null
                        );
                        return true;
                    }
                    else if (door?.DoorState == EDoorState.Open)
                    {
                        LootingBots.LootLog.LogDebug($"Bot {BotOwner.Id} Closing door");
                        GClass2599 interactionResult = new GClass2599(EInteractionType.Close);
                        BotOwner.SetTargetMoveSpeed(0f);
                        BotOwner.GetPlayer.CurrentState.StartDoorInteraction(
                            door,
                            interactionResult,
                            null
                        );
                        return true;
                    }
                }

                // Bot is stuck, update stuck count
                LootCache.UpdateStuckCount(BotOwner.Id);
            }
            else
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                botContainerData.Dist = dist;
                botContainerData.StuckCount = 0;
                LootCache.SetLootData(BotOwner.Id, botContainerData);
            }

            return false;
        }

        public bool IsCloseEnough(out float dist)
        {
            BotLootData lootData = LootCache.GetLootData(BotOwner.Id);
            Vector3 vector = BotOwner.Position - lootData.Destination;
            float y = vector.y;
            vector.y = 0f;
            dist = vector.magnitude;
            return dist < 0.5f;
        }

        public bool TryMoveToLoot(ref float tryMoveTimer)
        {
            try
            {
                // Stand and move to container
                BotOwner.SetPose(1f);
                BotOwner.SetTargetMoveSpeed(1f);
                BotOwner.Steering.LookToMovingDirection();

                if (tryMoveTimer < Time.time)
                {
                    BotLootData botLootData = LootCache.UpdateNavigationAttempts(BotOwner.Id);
                    string lootableName =
                        botLootData.ActiveContainer != null
                            ? botLootData.ActiveContainer.ItemOwner.Items.ToArray()[
                                0
                            ].Name.Localized()
                            : botLootData.ActiveItem.Name.Localized();

                    // If the bot has not been stuck for more than 4 navigation checks, attempt to navigate to the container otherwise ignore the container forever
                    if (botLootData.StuckCount <= 4)
                    {
                        tryMoveTimer = Time.time + 2f;
                        Vector3 center = botLootData.LootObjectCenter;

                        // Try to snap the desired destination point to the nearest NavMesh to ensure the bot can draw a navigable path to the point
                        Vector3 pointNearbyContainer = NavMesh.SamplePosition(
                            center,
                            out NavMeshHit navMeshAlignedPoint,
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
                        botLootData.Destination = pointNearbyContainer = NavMesh.SamplePosition(
                            center - padding,
                            out navMeshAlignedPoint,
                            1f,
                            navMeshAlignedPoint.mask
                        )
                            ? navMeshAlignedPoint.position
                            : pointNearbyContainer;

                        // Debug for bot container navigation
                        if (LootingBots.DebugLootNavigation.Value)
                        {
                            GameObjectHelper.DrawSphere(center, 0.5f, Color.red);
                            GameObjectHelper.DrawSphere(center - padding, 0.5f, Color.green);
                            if (pointNearbyContainer != Vector3.zero)
                            {
                                GameObjectHelper.DrawSphere(pointNearbyContainer, 0.5f, Color.blue);
                            }
                        }

                        // If we were able to snap the container position to a NavMesh, attempt to navigate
                        if (pointNearbyContainer != Vector3.zero)
                        {
                            NavMeshPathStatus pathStatus = BotOwner.GoToPoint(
                                pointNearbyContainer,
                                true,
                                -1f,
                                false,
                                false,
                                true
                            );

                            LootingBots.LootLog.LogDebug(
                                $"(Attempt: {botLootData.NavigationAttempts}) Bot {BotOwner.Id} moving to {lootableName} status: {pathStatus}"
                            );

                            if (pathStatus != NavMeshPathStatus.PathComplete)
                            {
                                LootingBots.LootLog.LogWarning(
                                    $"Bot {BotOwner.Id} has no valid path to: {lootableName}. Ignoring"
                                );
                                return false;
                            }

                            LootCache.SetLootData(BotOwner.Id, botLootData);
                        }
                        else
                        {
                            LootingBots.LootLog.LogWarning(
                                $"Bot {BotOwner.Id} unable to snap container position to NavMesh. Ignoring {lootableName}"
                            );
                            return false;
                        }
                    }
                    else
                    {
                        LootingBots.LootLog.LogWarning(
                            $"Bot {BotOwner.Id} maximum navigation attempts exceeded for: {lootableName}. Ignoring"
                        );
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e.Message);
                LootingBots.LootLog.LogError(e.StackTrace);
                return false;
            }
        }

        public void Destroy()
        {
            Destroy(this);
        }
    }
}
