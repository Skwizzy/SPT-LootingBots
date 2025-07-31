using System.Buffers;
using System.Collections;

using EFT;
using EFT.Interactive;

using LootingBots.Utilities;

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

        private static readonly ArrayPool<Collider> ColliderPool = ArrayPool<Collider>.Shared;

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
            Item = 2,
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
            StartCoroutine(FindLootCoroutine());
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

        public IEnumerator FindLootCoroutine()
        {
            IsScanRunning = true;

            Collider[] colliders = ColliderPool.Rent(3000);

            try
            {
                // Use the largest detection radius specified in the settings as the main Sphere radius
                float detectionRadius = Mathf.Max(DetectItemDistance, DetectContainerDistance, DetectCorpseDistance);
                var botPosition = _botOwner.Position;

                // Cast a sphere on the bot, detecting any Interactive world objects that collide with the sphere
                var hits = Physics.OverlapSphereNonAlloc(
                    _botOwner.Position,
                    detectionRadius,
                    colliders,
                    LootUtils.LootMask,
                    QueryTriggerInteraction.Ignore
                );

                yield return null;

                if (hits == 0)
                {
                    if (_log.DebugEnabled)
                    {
                        _log.LogDebug("No loot in range");
                    }
                    IsScanRunning = false;
                    yield break;
                }

                // Sort colliders by distance
                Array.Sort(colliders, 0, hits, new ColliderDistanceComparer(botPosition));

                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Scan results: {hits}");
                }

                yield return null;

                int rangeCalculations = 0;
                int maxRangeCalculations = 3;

                // Cache these values to avoid repeated property access
                var containerLootingEnabled = LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(_lootingBrain);
                var itemLootingEnabled = LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(_lootingBrain);
                var corpseLootingEnabled = LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(_lootingBrain);
                var availableGridSpaces = _lootingBrain.Stats.AvailableGridSpaces;
                var botName = _botOwner.name;

                // Process sorted colliders
                for (int i = 0; i < hits; i++)
                {
                    var collider = colliders[i];

                    if (collider is null || string.IsNullOrEmpty(botName))
                    {
                        yield return null;
                        continue;
                    }

                    // Get components once and reuse
                    var container = collider.gameObject.GetComponentInParent<LootableContainer>();
                    var lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                    var corpse = collider.gameObject.GetComponentInParent<Player>();
                    var rootItem = container?.ItemOwner?.RootItem ?? lootItem?.ItemOwner?.RootItem;

                    // If object has been ignored, skip to the next object detected
                    if (_lootingBrain.IsLootIgnored(rootItem?.Id))
                    {
                        yield return null;
                        continue;
                    }

                    bool canLootContainer =
                        containerLootingEnabled
                        && container != null // Container exists
                        && container.isActiveAndEnabled // Container is marked as active and enabled
                        && container.DoorState != EDoorState.Locked; // Container is not locked

                    bool canLootItem =
                        itemLootingEnabled
                        && lootItem is not null
                        && lootItem is not Corpse // Item is not a corpse
                        && rootItem is not null
                        && !rootItem.QuestItem // Item is not a quest item
                        && (
                            rootItem is SearchableItemItemClass // If the item is something that can be searched, consider it lootable
                            || (
                                rootItem is ArmoredEquipmentItemClass armor
                                && _lootingBrain.InventoryController.IsBetterArmorThanEquipped(armor)
                            )
                            || (_lootingBrain.IsValuableEnough(rootItem) && availableGridSpaces > rootItem.GetItemSize())
                        );

                    bool canLootCorpse =
                        corpseLootingEnabled
                        && corpse != null // Corpse exists
                        && corpse.GetPlayer != null; // Corpse is a bot corpse and not a static "Dead scav" corpse

                    if (!(canLootContainer || canLootItem || canLootCorpse))
                    {
                        yield return null;
                        continue;
                    }

                    var bounds = collider.bounds;
                    var center = new Vector3(bounds.center.x, bounds.center.y - bounds.extents.y - 0.4f, bounds.center.z);
                    var destination = GetDestination(center);

                    LootType lootType =
                        container != null ? LootType.Container
                        : lootItem != null ? LootType.Item
                        : LootType.Corpse;

                    // Check if loot is in range and sight
                    if (!IsLootInRange(lootType, destination, out float dist) || !IsLootInSight(lootType, destination))
                    {
                        if (dist != -1 && ++rangeCalculations >= maxRangeCalculations)
                        {
                            if (_log.DebugEnabled)
                                _log.LogDebug("No loot in range");
                            break;
                        }
                        yield return null;
                        continue;
                    }

                    // Cache the loot and set active target
                    ActiveLootCache.CacheActiveLootId(rootItem.Id, _botOwner);
                    _lootingBrain.DistanceToLoot = dist;
                    _lootingBrain.Destination = destination;

                    if (canLootContainer)
                    {
                        _lootingBrain.ActiveContainer = container;
                        _lootingBrain.LootObjectPosition = container.transform.position;
                    }
                    else if (canLootCorpse)
                    {
                        _lootingBrain.ActiveCorpse = corpse;
                        _lootingBrain.LootObjectPosition = corpse.Transform.position;
                    }
                    else
                    {
                        _lootingBrain.ActiveItem = lootItem;
                        _lootingBrain.LootObjectPosition = lootItem.transform.position;
                    }

                    break;
                }
            }
            finally
            {
                ColliderPool.Return(colliders, true);
                IsScanRunning = false;
            }
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
                    _log.LogWarning("botOwner.BotMover is null! Cannot perform path distance calculations");
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
                    _log.LogWarning("botOwner.LookSensor is null! Cannot perform line of sight check");
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

        private Vector3 GetDestination(Vector3 center)
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
