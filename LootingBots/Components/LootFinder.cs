using System.Buffers;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using LootingBots.Patches;
using LootingBots.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace LootingBots.Components;

public class LootFinder : MonoBehaviour
{
    private static readonly ArrayPool<Collider> _colliderPool = ArrayPool<Collider>.Shared;

    private LootingBrain _lootingBrain;
    private BotOwner _botOwner;
    private BotLog _log;

    private float _scanTimer;
    private bool _lockUntilNextScan;

    private const int MaxEmptyAttempts = 3;
    private const float EmptyAttemptsCooldown = 180f;
    private int _emptyAttempts;

    // TODO: Add empty attempts config?

    // Bot specific config
    private bool _containerLootingEnabled;
    private bool _needsContainerSight;
    private bool _itemLootingEnabled;
    private bool _needsItemSight;
    private bool _corpseLootingEnabled;
    private bool _needsCorpseSight;

    public bool IsScheduledScan
    {
        get { return _scanTimer < Time.time; }
    }

    private static float DetectCorpseDistance
    {
        get { return LootingBots.DetectCorpseDistance.Value; }
    }

    private static float DetectContainerDistance
    {
        get { return LootingBots.DetectContainerDistance.Value; }
    }

    private static float DetectItemDistance
    {
        get { return LootingBots.DetectItemDistance.Value; }
    }

    public enum LootType : byte
    {
        None = 0,
        Corpse = 1,
        Container = 2,
        Item = 3,
    }

    public bool IsScanRunning { get; private set; }
    private CancellationTokenSource _lootFinderCts;

    private readonly Queue<LootableContainer> _priorityLootableContainers = [];
    private readonly Queue<Player> _priorityCorpses = [];

    public void Init(BotOwner botOwner)
    {
        _scanTimer = Time.time + LootingBots.InitialStartTimer.Value;
        _botOwner = botOwner;
        _lootingBrain = _botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
        _log = new BotLog(LootingBots.LootLog, _botOwner);

        _containerLootingEnabled = LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(_lootingBrain);
        _needsContainerSight = LootingBots.DetectContainerNeedsSight.Value.IsBotEnabled(_lootingBrain);
        _itemLootingEnabled = LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(_lootingBrain);
        _needsItemSight = LootingBots.DetectItemNeedsSight.Value.IsBotEnabled(_lootingBrain);
        _corpseLootingEnabled = LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(_lootingBrain);
        _needsCorpseSight = LootingBots.DetectCorpseNeedsSight.Value.IsBotEnabled(_lootingBrain);

        if (_containerLootingEnabled)
        {
            OnAirdropLandedPatch.OnAirdropLanded += OnAirdropLanded;
        }

        if (_corpseLootingEnabled)
        {
            botOwner.BotPersonalStats.OnKillTarget += OnKilledEnemyPlayer;
        }
    }

    public void ResetScanTimer()
    {
        // If the loot finder is locked, do not reset it
        if (!_lockUntilNextScan)
        {
            _scanTimer = Time.time + LootingBots.LootScanInterval.Value;
        }
    }

    public void BeginSearch(int ticket)
    {
        IsScanRunning = true;

        StopFindingLoot();
        _lootFinderCts = new CancellationTokenSource();
        if (!FindPrioritizedLoot(ticket))
        {
            _ = FindLootAsync(ticket, _lootFinderCts.Token).ContinueWith(ExceptionHandler, TaskScheduler.Current);
        }

        SetLockUntilNextScan(false);
    }

    public void ForceScan()
    {
        _scanTimer = Time.time - 1f;
        SetLockUntilNextScan(true);
        _lootingBrain.ForceBrainEnabled = true;
    }

    public void OverrideNextScanTime(float scanTime)
    {
        _scanTimer = Time.time + scanTime;
        SetLockUntilNextScan(true);
    }

    public void SetLockUntilNextScan(bool value)
    {
        _lockUntilNextScan = value;
    }

    public void StopFindingLoot()
    {
        if (_lootFinderCts is null)
        {
            return;
        }

        _lootFinderCts.Cancel();
        _lootFinderCts.Dispose();
        _lootFinderCts = null;
    }

    public void OnDestroy()
    {
        StopFindingLoot();

        if (_containerLootingEnabled)
        {
            OnAirdropLandedPatch.OnAirdropLanded -= OnAirdropLanded;
        }

        if (_corpseLootingEnabled)
        {
            _botOwner.BotPersonalStats.OnKillTarget -= OnKilledEnemyPlayer;
        }
    }

    private async Task FindLootAsync(int queue, CancellationToken token)
    {
        IsScanRunning = true;

        var colliders = _colliderPool.Rent(3000);

        try
        {
            if (_botOwner == null)
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug("BotOwner is NULL, cannot start scan!");
                }
                return;
            }

            // Use the largest detection radius specified in the settings as the main Sphere radius
            var detectionRadius = Mathf.Max(DetectItemDistance, DetectContainerDistance);
            detectionRadius = Mathf.Max(detectionRadius, DetectCorpseDistance);
            var botPosition = _botOwner.Position;

            // Cast a sphere on the bot, detecting any Interactive world objects that collide with the sphere
            var hits = Physics.OverlapSphereNonAlloc(
                _botOwner.Position,
                detectionRadius,
                colliders,
                LootUtils.LootMask,
                QueryTriggerInteraction.Ignore
            );

            await Task.Yield();

            if (hits == 0)
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug("No loot in range");
                }
                return;
            }

            // Sort colliders by distance
            Array.Sort(colliders, 0, hits, new ColliderDistanceComparer(botPosition));

            if (_log.DebugEnabled)
            {
                _log.LogDebug($"Scan results: {hits}");
            }

            await Task.Yield();

            const int maxRangeCalculations = 3;
            var rangeCalculations = 0;
            var availableGridSpaces = _lootingBrain.Stats.AvailableGridSpaces;

            // Process sorted colliders
            for (var i = 0; i < hits; i++)
            {
                token.ThrowIfCancellationRequested();

                var collider = colliders[i];

                Item rootItem = null;
                var lootType = LootType.None;

                // Get InteractableObject once and check derived type
                var interactableObject = collider.gameObject.GetComponentInParent<InteractableObject>();
                if (_corpseLootingEnabled && interactableObject is Corpse corpse)
                {
                    var player = collider.gameObject.GetComponentInParent<Player>();
                    if (
                        player != null // Corpse is a bot corpse and not a static "Dead scav"
                        && corpse.ItemOwner?.RootItem is InventoryEquipment equipment
                    )
                    {
                        rootItem = equipment;
                        lootType = LootType.Corpse;
                    }
                }
                else if (_containerLootingEnabled && interactableObject is LootableContainer container)
                {
                    rootItem = container.ItemOwner?.RootItem;
                    if (
                        container.isActiveAndEnabled // Container is marked as active and enabled
                        && container.DoorState is not EDoorState.Locked // Container is not locked
                    )
                    {
                        lootType = LootType.Container;
                    }
                }
                else if (_itemLootingEnabled && interactableObject is LootItem lootItem && lootItem is not Corpse)
                {
                    rootItem = lootItem.ItemOwner?.RootItem;
                    if (
                        rootItem is not null
                        && !rootItem.QuestItem // Item is not a quest item
                        && (
                            rootItem is EFT.InventoryLogic.SearchableItem // If the item is something that can be searched, consider it lootable
                            || (
                                rootItem is EFT.InventoryLogic.ArmoredEquipment armor
                                && _lootingBrain.InventoryController.IsBetterArmorThanEquipped(armor)
                            )
                            || (_lootingBrain.IsValuableEnough(rootItem) && availableGridSpaces > rootItem.GetItemSize())
                        )
                    )
                    {
                        lootType = LootType.Item;
                    }
                }

                await Task.Yield();

                if (lootType is LootType.None || rootItem is null)
                {
                    await Task.Yield();

                    continue;
                }

                // If object has been ignored, skip to the next object detected
                var rootItemId = rootItem.Id;
                if (_lootingBrain.IsLootIgnored(rootItemId) || ActiveLootCache.IsLootInUse(rootItemId))
                {
                    await Task.Yield();

                    continue;
                }

                var bounds = collider.bounds;
                var center = new Vector3(bounds.center.x, bounds.center.y - bounds.extents.y - 0.4f, bounds.center.z);
                var destination = GetDestination(center);

                await Task.Yield();

                // Check if we can perform distance and LOS checks
                if (_botOwner.Mover == null)
                {
                    if (_log.WarningEnabled)
                    {
                        _log.LogWarning("botOwner.BotMover is null! Cannot perform path distance calculations");
                    }

                    break;
                }
                if (_botOwner.LookSensor == null)
                {
                    if (_log.WarningEnabled)
                    {
                        _log.LogWarning("botOwner.LookSensor is null! Cannot perform line of sight check");
                    }

                    break;
                }

                // Check if loot is in range
                if (!IsLootInRange(lootType, destination, out var dist))
                {
                    // Found path to loot but not within range
                    if (dist != -1f && ++rangeCalculations >= maxRangeCalculations)
                    {
                        if (_log.DebugEnabled)
                        {
                            _log.LogDebug("No loot in range, reached max calculations");
                        }

                        break;
                    }
                    await Task.Yield();

                    continue;
                }

                // Check if loot is in sight
                if (!IsLootInSight(lootType, destination))
                {
                    await Task.Yield();

                    continue;
                }

                // Cache the loot and set active target
                if (!ActiveLootCache.CacheActiveLootId(rootItemId, _botOwner))
                {
                    if (_log.ErrorEnabled)
                    {
                        _log.LogError("Failed to cache and set active loot, bot owner is null or id already in the cache?");
                    }
                    await Task.Yield();

                    continue;
                }

                _lootingBrain.SetLoot(interactableObject, lootType, interactableObject.transform.position, destination, dist);
                _emptyAttempts = 0;
                break;
            }
        }
        finally
        {
            if (!_lootingBrain.HasActiveLootable && ++_emptyAttempts > MaxEmptyAttempts)
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Max empty attempts reached, preventing looting for {EmptyAttemptsCooldown}s");
                }
                OverrideNextScanTime(EmptyAttemptsCooldown);
                _emptyAttempts = 0;
            }

            _colliderPool.Return(colliders, true);
            ScanScheduler.Return(queue);
            _lootingBrain.ForceBrainEnabled = false;
            IsScanRunning = false;
        }
    }

    public bool FindPrioritizedLoot(int ticket)
    {
        for (var i = 0; i < _priorityLootableContainers.Count; i++)
        {
            var lootableContainer = _priorityLootableContainers.Dequeue();

            var position = lootableContainer.TrackableTransform.position;
            var destination = GetDestination(position);

            if (!IsLootInRange(LootType.Container, destination, out var dist))
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Re-queuing container [{lootableContainer.GetLootName()}], not in range. Dist: {dist}");
                }
                _priorityLootableContainers.Enqueue(lootableContainer);
                continue;
            }

            // Cache the loot and set active target
            if (!ActiveLootCache.CacheActiveLootId(lootableContainer.GetRootItemId(), _botOwner))
            {
                if (_log.ErrorEnabled)
                {
                    _log.LogError("Failed to cache and set active loot, bot owner is null or id already in the cache?");
                }
                continue;
            }

            _lootingBrain.SetLoot(lootableContainer, LootType.Container, position, destination, dist);

            if (_log.DebugEnabled)
            {
                _log.LogDebug($"Setting container [{lootableContainer.GetLootName()}] as active loot. Dist: {dist}");
            }

            ScanScheduler.Return(ticket);
            _lootingBrain.ForceBrainEnabled = false;
            IsScanRunning = false;
            return true;
        }

        for (var i = 0; i < _priorityCorpses.Count; i++)
        {
            var player = _priorityCorpses.Dequeue();
            if (_log.DebugEnabled)
            {
                _log.LogDebug($"Trying to find prioritized corpse: {player.AIData?.BotOwner.Name()}");
            }

            var corpse = LootUtils._playerCorpseField(player);
            if (corpse == null)
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Removing prioritized player, corpse not found for killed player [{player.AIData?.BotOwner.Name()}]");
                }

                continue;
            }

            // If corpse has been ignored, continue to the next prioritized corpse
            var rootItemId = corpse.GetRootItemId();
            if (_lootingBrain.IsLootIgnored(rootItemId))
            {
                continue;
            }
            if (ActiveLootCache.IsLootInUse(rootItemId))
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Re-queuing corpse [{corpse.GetLootName()}], is currently being looted by someone else");
                }
                _priorityCorpses.Enqueue(player);
                continue;
            }

            var position = corpse.TrackableTransform.position;
            var destination = GetDestination(position);

            // Check if loot is in range
            // No need to check LOS since technically it's their kill
            if (!IsLootInRange(LootType.Corpse, destination, out var dist))
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Re-queuing corpse [{corpse.GetLootName()}], not in range. Dist: {dist}");
                }
                _priorityCorpses.Enqueue(player);
                continue;
            }

            // Cache the loot and set active target
            if (!ActiveLootCache.CacheActiveLootId(rootItemId, _botOwner))
            {
                if (_log.ErrorEnabled)
                {
                    _log.LogError("Failed to cache and set active loot, bot owner is null or id already in the cache?");
                }
                continue;
            }

            _lootingBrain.SetLoot(corpse, LootType.Corpse, position, destination, dist);

            if (_log.DebugEnabled)
            {
                _log.LogDebug($"Setting Corpse [{corpse.GetLootName()}] as active loot. Dist: {dist}");
            }

            ScanScheduler.Return(ticket);
            _lootingBrain.ForceBrainEnabled = false;
            IsScanRunning = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks to see if any of the found lootable items are within their detection range specified in the mod settings.
    /// </summary>
    private bool IsLootInRange(LootType lootType, Vector3 destination, out float dist)
    {
        if (destination == Vector3.zero)
        {
            dist = -1f;
            return false;
        }

        var maxRange = lootType switch
        {
            LootType.Corpse => DetectCorpseDistance,
            LootType.Container => DetectContainerDistance,
            LootType.Item => DetectItemDistance,
            LootType.None => throw new ArgumentOutOfRangeException(nameof(lootType), lootType, null),
            _ => throw new ArgumentOutOfRangeException(nameof(lootType), lootType, null),
        };

        var path = _botOwner.Mover.CalcPath(destination);
        return path.CalculatePathLengthWithMaxRange(maxRange, out dist);
    }

    private bool IsLootInSight(LootType lootType, Vector3 destination)
    {
        var needsSight = lootType switch
        {
            LootType.Corpse => _needsCorpseSight,
            LootType.Container => _needsContainerSight,
            LootType.Item => _needsItemSight,
            LootType.None => throw new ArgumentOutOfRangeException(nameof(lootType), lootType, null),
            _ => throw new ArgumentOutOfRangeException(nameof(lootType), lootType, null),
        };
        if (!needsSight)
        {
            return true;
        }

        if (destination == Vector3.zero)
        {
            return false;
        }

        var start = _botOwner.LookSensor.HeadPoint;
        var directionOfLoot = destination - start;

        var sightBlocked = Physics.Raycast(start, directionOfLoot, directionOfLoot.magnitude, LayersMaskController.HighPolyWithTerrainMask);

        return !sightBlocked;
    }

    private static Vector3 GetDestination(Vector3 center)
    {
        // Try to snap the desired destination point to the nearest NavMesh to ensure the bot can draw a navigable path to the point
        var pointNearbyContainer = NavMesh.SamplePosition(center, out var navMeshAlignedPoint, 1f, NavMesh.AllAreas)
            ? navMeshAlignedPoint.position
            : Vector3.zero;

        // Since SamplePosition always snaps to the closest point on the NavMesh, sometimes this point is a little too close to the loot and causes the bot to shake violently while looting.
        // Add a small amount of padding by pushing the point away from the nearbyPoint
        var padding = center - pointNearbyContainer;
        padding.y = 0;
        padding.Normalize();

        // Make sure the point is still snapped to the NavMesh after its been pushed
        var destination = NavMesh.SamplePosition(center - (padding * 1.5f), out navMeshAlignedPoint, 1f, navMeshAlignedPoint.mask)
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

    private void OnAirdropLanded(LootableContainer airdrop)
    {
        if(_log.DebugEnabled)
        {
            _log.LogDebug($"Adding [{airdrop.GetLootName()}] to priority queue");
        }

        _priorityLootableContainers.Enqueue(airdrop);
    }

    private void OnKilledEnemyPlayer(string victimProfileId, EFT.Ballistics.DamageInfo damageInfo)
    {
        var playerOwner = Singleton<GameWorld>.Instance.GetEverExistedBridgeByProfileID(victimProfileId);
        if (playerOwner?.iPlayer is Player victimPlayer)
        {
            _priorityCorpses.Enqueue(victimPlayer);
        }
        else
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError($"Killed player not found! Victim ProfileId: {victimProfileId}");
            }
        }
    }

    private void ExceptionHandler(Task task)
    {
        if (task.IsCanceled)
        {
            if (_log.DebugEnabled)
            {
                _log.LogDebug("Loot scan interrupted");
            }
            return;
        }

        if (task.IsFaulted)
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError("Exception while trying to scan for loot:");
                _log.LogError(task.Exception!.ToString());
            }
        }
    }
}

public static class PathExtensions
{
    /// <summary>
    /// Based on <see cref="NavMeshPathExtension.CalculatePathLength(Vector3[] corners)"/>
    /// </summary>
    public static bool CalculatePathLengthWithMaxRange(this Vector3[] corners, float range, out float length)
    {
        if (corners == null || corners.Length < 2)
        {
            length = -1f;
            return false;
        }

        length = 0f;
        var prevCorner = corners[0];
        for (var i = 1; i < corners.Length; i++)
        {
            var currentCorner = corners[i];
            var vector3 = prevCorner - currentCorner;
            length += Mathf.Sqrt(vector3.x * vector3.x + vector3.y * vector3.y + vector3.z * vector3.z);

            // Reached max range
            if (length > range)
            {
                return false;
            }

            prevCorner = currentCorner;
        }

        return true;
    }
}
