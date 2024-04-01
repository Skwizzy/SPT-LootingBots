using System;
using System.Collections;
using System.Collections.Generic;
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
        LootingBrain _lootingBrain;
        BotOwner _botOwner;
        BotLog _log;
        public float ScanTimer;
        public bool LockUntilNextScan;

        public bool IsScheduledScan
        {
            get { return ScanTimer < Time.time; }
        }

        private float DetectCorpseDistance
        {
            get { return LootingBots.DetectCorpseDistance.Value; }
        }
        private float DetectContainerDistance
        {
            get { return LootingBots.DetectContainerDistance.Value; }
        }
        private float DetectItemDistance
        {
            get { return LootingBots.DetectItemDistance.Value; }
        }

        public enum LootType
        {
            Corpse = 0,
            Container = 1,
            Item = 2
        }

        public bool IsScanRunning = false;

        public void Init(BotOwner botOwner)
        {
            ScanTimer = Time.time + LootingBots.InitialStartTimer.Value;
            _botOwner = botOwner;
            _lootingBrain = _botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
            _log = new BotLog(LootingBots.LootLog, _botOwner);
        }

        public void ResetScanTimer()
        {
            // If the loot finder is locked, do not reset it
            if (!LockUntilNextScan)
            {
                ScanTimer = Time.time + LootingBots.LootScanInterval.Value;
            }
        }

        public void BeginSearch()
        {
            StartCoroutine(FindLootable());
            LockUntilNextScan = false;
        }

        public IEnumerator FindLootable()
        {
            IsScanRunning = true;
            // Use the largest detection radius specified in the settings as the main Sphere radius
            float detectionRadius = Mathf.Max(
                DetectItemDistance,
                DetectContainerDistance,
                DetectCorpseDistance
            );

            // Cast a sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] colliders = Physics.OverlapSphere(
                _botOwner.Position,
                detectionRadius,
                LootUtils.LootMask,
                QueryTriggerInteraction.Collide
            );

            // Sort by nearest to bot location
            List<Collider> colliderList = colliders.ToList();
            colliderList.Sort(
                (a, b) =>
                {
                    var distA = Vector3.Distance(a.bounds.center, _botOwner.Position);
                    var distB = Vector3.Distance(b.bounds.center, _botOwner.Position);
                    return distA.CompareTo(distB);
                }
            );

            int rangeCalculations = 0;

            // For each object detected, check to see if it is loot and then calculate its distance from the player
            foreach (Collider collider in colliderList)
            {
                if (collider == null || String.IsNullOrEmpty(_botOwner.name))
                {
                    continue;
                }

                LootableContainer container =
                    collider.gameObject.GetComponentInParent<LootableContainer>();
                LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                BotOwner corpse = collider.gameObject.GetComponentInParent<BotOwner>();
                Item rootItem = container?.ItemOwner?.RootItem ?? lootItem?.ItemOwner?.RootItem;
                // If object has been ignored, skip to the next object detected
                if (_lootingBrain.IsLootIgnored(rootItem?.Id))
                {
                    continue;
                }

                bool canLootContainer =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(_lootingBrain)
                    && container != null // Container exists
                    && container.isActiveAndEnabled // Container is marked as active and enabled
                    && container.DoorState != EDoorState.Locked; // Container is not locked

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(_lootingBrain)
                    && !(lootItem is Corpse) // Item is not a corpse
                    && !rootItem.QuestItem // Item is not a quest item
                    && (
                        lootItem?.ItemOwner?.RootItem is SearchableItemClass // If the item is something that can be searched, consider it lootable
                        || (
                            _lootingBrain.IsValuableEnough(rootItem) // Otherwise, bot must have enough space to pickup and item must meet value the threshold
                            && _lootingBrain.Stats.AvailableGridSpaces > rootItem.GetItemSize()
                        )
                    );

                bool canLootCorpse =
                    LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(_lootingBrain)
                    && corpse != null // Corpse exists
                    && corpse.GetPlayer != null; // Corpse is a bot corpse and not a static "Dead scav" corpse

                if (canLootContainer || canLootItem || canLootCorpse)
                {
                    Vector3 center = collider.bounds.center;
                    // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                    center.y = collider.bounds.center.y - collider.bounds.extents.y - 0.4f;

                    LootType lootType =
                        container != null
                            ? LootType.Container
                            : lootItem != null
                                ? LootType.Item
                                : LootType.Corpse;

                    // If we havent already visted the lootable, calculate its distance and save the lootable with the shortest distance
                    Vector3 destination = GetDestination(center);

                    // If we are considering a lootable to be the new closest lootable, make sure the loot is in the detection range specified for the type of loot
                    if (IsLootInRange(lootType, destination, out float dist))
                    {
                        ActiveLootCache.CacheActiveLootId(rootItem.Id, _botOwner);

                        if (canLootContainer)
                        {
                            _lootingBrain.ActiveContainer = container;
                            _lootingBrain.LootObjectPosition = container.transform.position;
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
                        else if (canLootCorpse)
                        {
                            _lootingBrain.ActiveCorpse = corpse;
                            _lootingBrain.LootObjectPosition = corpse.Transform.position;
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
                        else
                        {
                            _lootingBrain.ActiveItem = lootItem;
                            _lootingBrain.LootObjectPosition = lootItem.transform.position;
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
                    }
                    else if (dist != -1)
                    {
                        rangeCalculations++;
                    }

                    if (rangeCalculations == 3)
                    {
                        if (_log.DebugEnabled)
                            _log.LogDebug("No loot in range");

                        break;
                    }
                }

                yield return null;
            }

            IsScanRunning = false;
        }

        /**
        * Checks to see if any of the found lootable items are within their detection range specified in the mod settings.
        */
        public bool IsLootInRange(LootType lootType, Vector3 destination, out float dist)
        {
            bool isContainer = lootType == LootType.Container;
            bool isItem = lootType == LootType.Item;
            bool isCorpse = lootType == LootType.Corpse;

            if (destination == Vector3.zero || _botOwner?.Mover == null)
            {
                if (_botOwner?.Mover == null && _log.WarningEnabled)
                {
                    _log.LogWarning(
                        "botOwner.BotMover is null! Cannot perform path distance calculations"
                    );
                }
                dist = -1f;
                return false;
            }

            dist = _botOwner.Mover.ComputePathLengthToPoint(destination);
            return (isContainer && dist <= DetectContainerDistance)
                || (isItem && dist <= DetectItemDistance)
                || (isCorpse && dist <= DetectCorpseDistance);
        }

        Vector3 GetDestination(Vector3 center)
        {
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
            Vector3 destination = NavMesh.SamplePosition(
                center - padding,
                out navMeshAlignedPoint,
                1f,
                navMeshAlignedPoint.mask
            )
                ? navMeshAlignedPoint.position
                : pointNearbyContainer;

            if (LootingBots.DebugLootNavigation.Value)
            {
                GameObjectHelper.DrawSphere(center, 0.5f, Color.red);
                GameObjectHelper.DrawSphere(pointNearbyContainer, 0.5f, Color.green);
                GameObjectHelper.DrawSphere(destination, 0.5f, Color.blue);
            }

            return destination;
        }
    }
}
