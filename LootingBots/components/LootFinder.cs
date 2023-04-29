using System;
using System.Linq;
using System.Reflection;

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
            try
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

                if (BotOwner.BotState == EBotState.Active)
                {
                    BotLootData botLootData = LootCache.GetLootData(BotOwner.Id);

                    if (ItemAdder == null)
                    {
                        ItemAdder = new ItemAdder(BotOwner);
                    }

                    BotOwner.DoorOpener.Update();

                    if (
                        botLootData.WaitAfterLooting < Time.time
                        && _scanTimer < Time.time
                        && !botLootData.HasActiveLootable()
                    )
                    {
                        FindLootable();
                    }
                }
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e);
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
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && containerObj != null
                    && !LootCache.IsLootIgnored(BotOwner.Id, containerObj.Id)
                    && containerObj.isActiveAndEnabled
                    && containerObj.DoorState != EDoorState.Locked;

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && lootItem != null
                    && lootItem?.ItemOwner?.RootItem != null
                    && !lootItem.ItemOwner.RootItem.QuestItem
                    && !LootCache.IsLootIgnored(BotOwner.Id, lootItem.ItemOwner.RootItem.Id);

                if (canLootContainer || canLootItem)
                {
                    // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                    Vector3 vector =
                        BotOwner.Position
                        - (containerObj?.transform?.position ?? lootItem.transform.position);
                    float dist = vector.sqrMagnitude;

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

            // Close container and switch to main weapon
            FieldInfo movementContextInfo = BotOwner.GetPlayer.CurrentState
                .GetType()
                .GetField("MovementContext", BindingFlags.NonPublic | BindingFlags.Instance);
            var movementContext = (GClass1604)
                movementContextInfo.GetValue(BotOwner.GetPlayer.CurrentState);
            movementContext.ReleaseDoorIfInteractingWithOne();

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
            LootCache.Cleanup(BotOwner, item.Id);
            LootCache.AddVisitedLoot(BotOwner.Id, item.Id);
        }

        public void CheckIfStuck(float dist)
        {
            BotLootData botContainerData = LootCache.GetLootData(BotOwner.Id);

            // Calculate change in distance and assume any change less than .25f means the bot hasnt moved.
            float changeInDist = Math.Abs(botContainerData.Dist - dist);

            if (changeInDist < 0.25f)
            {
                LootingBots.LootLog.LogDebug(
                    $"(Stuck: {botContainerData.StuckCount}) Bot {BotOwner.Id} has not moved {changeInDist}. Dist from loot: {dist}"
                );

                // Bot is stuck, update stuck count
                LootCache.UpdateStuckCount(BotOwner.Id);
            }
            else
            {
                // Bot has moved, reset stuckCount and update cached distance to container
                botContainerData.Dist = dist;
                botContainerData.StuckCount = 0;
            }

            LootCache.SetLootData(BotOwner.Id, botContainerData);
        }

        public bool IsCloseEnough(out float dist)
        {
            BotLootData lootData = LootCache.GetLootData(BotOwner.Id);
            Vector3 vector = BotOwner.Position - lootData.Destination;
            float y = vector.y;
            vector.y = 0f;
            dist = vector.sqrMagnitude;
            return dist < 0.85f && Math.Abs(y) < 0.5f;
        }

        public bool TryMoveToLoot(ref float tryMoveTimer)
        {
            bool canMove = true;
            try
            {
                // Stand and move to lootable
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

                    // If the bot has not been stuck for more than 2 navigation checks, attempt to navigate to the lootable otherwise ignore the container forever
                    if (botLootData.StuckCount < 1 && botLootData.NavigationAttempts <= 30)
                    {
                        tryMoveTimer = Time.time + 4f;
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

                        // Since SamplePosition always snaps to the closest point on the NavMesh, sometimes this point is a little too close to the loot and causes the bot to shake violently while looting.
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

                        // Debug for bot loot navigation
                        if (LootingBots.DebugLootNavigation.Value)
                        {
                            GameObjectHelper.DrawSphere(center, 0.5f, Color.red);
                            GameObjectHelper.DrawSphere(center - padding, 0.5f, Color.green);
                            if (pointNearbyContainer != Vector3.zero)
                            {
                                GameObjectHelper.DrawSphere(pointNearbyContainer, 0.5f, Color.blue);
                            }
                        }

                        // If we were able to snap the loot position to a NavMesh, attempt to navigate
                        if (pointNearbyContainer != Vector3.zero)
                        {
                            NavMeshPathStatus pathStatus = BotOwner.GoToPoint(
                                pointNearbyContainer,
                                true,
                                1f,
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
                                canMove = false;
                            }

                            LootCache.SetLootData(BotOwner.Id, botLootData);
                        }
                        else
                        {
                            LootingBots.LootLog.LogWarning(
                                $"Bot {BotOwner.Id} unable to snap loot position to NavMesh. Ignoring {lootableName}"
                            );
                            canMove = false;
                        }
                    }
                    else
                    {
                        LootingBots.LootLog.LogError(
                            $"Bot {BotOwner.Id} Has been stuck trying to reach for too long: {lootableName}. Ignoring"
                        );
                        canMove = false;
                    }
                }
            }
            catch (Exception e)
            {
                LootingBots.LootLog.LogError(e.Message);
                LootingBots.LootLog.LogError(e.StackTrace);
            }

            if (!canMove)
            {
                HandleNonNavigableLoot();
            }

            return canMove;
        }

        private void HandleNonNavigableLoot()
        {
            BotLootData botLootData = LootCache.GetLootData(BotOwner.Id);
            string lootId =
                botLootData.ActiveContainer != null
                    ? botLootData.ActiveContainer.ItemOwner.Items.ToArray()[0].Id
                    : botLootData.ActiveItem.ItemOwner.RootItem.Id;
            LootCache.Cleanup(BotOwner, lootId);
            LootCache.AddNonNavigableLoot(BotOwner.Id, lootId);
            LootCache.IncrementLootTimer(BotOwner.Id, 30f);
            BotOwner.PatrollingData.MoveUpdate();
        }

        public void Destroy()
        {
            Destroy(this);
        }
    }
}
