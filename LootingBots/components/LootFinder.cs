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

        public bool IsScanRunning;

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
            IsScanRunning = true;
            StartCoroutine(FindLootable());
            LockUntilNextScan = false;
        }

        public void ForceScan()
        {
            ScanTimer = Time.time - 1f;
            LockUntilNextScan = true;
            _lootingBrain.ForceBrainEnabled = true;
        }

        public void OverrideNextScanTime(float scanTime)
        {
            ScanTimer = Time.time + scanTime;
            LockUntilNextScan = true;
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
            Collider[] colliders = new Collider[3000];

            var hits = Physics.OverlapSphereNonAlloc(
                _botOwner.Position,
                detectionRadius,
                colliders,
                LootUtils.LootMask,
                QueryTriggerInteraction.Ignore
            );

            yield return null;

            // If we have some hits from the sphere overlap, process the results
            if (hits > 0)
            {
                List<Collider> colliderList = new(hits);
                int count = 0;

                // Collect the first 'hits' colliders
                foreach (Collider collider in colliders.Take(hits))
                {
                    float distance = Vector3.Distance(collider.bounds.center, _botOwner.Position);

                    // Insert in sorted order using a simple insertion sort
                    int insertIndex = 0;
                    while (insertIndex < count && Vector3.Distance(colliderList[insertIndex].bounds.center, _botOwner.Position) < distance)
                    {
                        insertIndex++;
                    }

                    // Insert the collider in the appropriate position, nearest should be first.
                    colliderList.Insert(insertIndex, collider);
                    count++;

                    if (count == hits) break;
                }

                // Optional logging if DebugEnabled
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Scan results: {colliderList.Count}");
                }

                yield return null;

                int rangeCalculations = 0;
                // For each object detected, check to see if it is loot and then calculate its distance from the player
                foreach (var collider in colliderList)
                {
                    if (collider == null || String.IsNullOrEmpty(_botOwner.name))
                    {
                        yield return null;
                        continue;
                    }

                    LootableContainer container =
                        collider.gameObject.GetComponentInParent<LootableContainer>();
                    LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                    Player corpse = collider.gameObject.GetComponentInParent<Player>();
                    Item rootItem = container?.ItemOwner?.RootItem ?? lootItem?.ItemOwner?.RootItem;
                    // If object has been ignored, skip to the next object detected
                    if (_lootingBrain.IsLootIgnored(rootItem?.Id))
                    {
                        yield return null;
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
                            lootItem?.ItemOwner?.RootItem is SearchableItemItemClass // If the item is something that can be searched, consider it lootable
                            || (
                                lootItem?.ItemOwner?.RootItem is ArmoredEquipmentItemClass newArmor // If the item is some sort of armor, check to see if its better than what is equipped
                                && _lootingBrain.InventoryController.IsBetterArmorThanEquipped(
                                    newArmor
                                )
                            )
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
                        if (
                            IsLootInRange(lootType, destination, out float dist)
                            && IsLootInSight(lootType, destination)
                        )
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
            }
            else if (_log.DebugEnabled)
            {
                _log.LogDebug("No loot in range");
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

        public bool IsLootInSight(LootType lootType, Vector3 destination)
        {
            bool isContainer = lootType == LootType.Container;
            bool isItem = lootType == LootType.Item;
            bool isCorpse = lootType == LootType.Corpse;

            if (
                LootingBots.DetectContainerNeedsSight.Value == false && isContainer
                || LootingBots.DetectItemNeedsSight.Value == false && isItem
                || LootingBots.DetectCorpseNeedsSight.Value == false && isCorpse
            )
            {
                return true;
            }

            if (destination == Vector3.zero || _botOwner?.LookSensor == null)
            {
                if (_botOwner?.LookSensor == null && _log.WarningEnabled)
                {
                    _log.LogWarning(
                        "botOwner.LookSensor is null! Cannot perform line of sight check"
                    );
                }
                return true;
            }

            Vector3 start = _botOwner.LookSensor._headPoint;
            Vector3 directionOfLoot = destination - start;

            bool sightBlocked = Physics.Raycast(
                start,
                directionOfLoot,
                directionOfLoot.magnitude,
                LayerMaskClass.HighPolyWithTerrainMask
            );

            return !sightBlocked;
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
                center - (padding * 1.5f),
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
