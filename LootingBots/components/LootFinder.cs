using System.Collections;
using System.Collections.Generic;
using System.Linq;

using EFT;
using EFT.Interactive;

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
            _botOwner = botOwner;
            _lootingBrain = _botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
            _log = new BotLog(LootingBots.LootLog, _botOwner);
        }

        public void BeginSearch()
        {
            StartCoroutine(FindLootable());
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

            // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
            foreach (Collider collider in colliderList)
            {
                if (collider == null)
                {
                    continue;
                }

                LootableContainer container =
                    collider.gameObject.GetComponentInParent<LootableContainer>();
                LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                BotOwner corpse = collider.gameObject.GetComponentInParent<BotOwner>();

                bool canLootContainer =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        _botOwner.Profile.Info.Settings.Role
                    )
                    && container != null // Container exists
                    && !_lootingBrain.IsLootIgnored(container.Id) // Container is not ignored
                    && container.isActiveAndEnabled // Container is marked as active and enabled
                    && container.DoorState != EDoorState.Locked; // Container is not locked

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        _botOwner.Profile.Info.Settings.Role
                    )
                    && lootItem != null
                    && !(lootItem is Corpse) // Item is not a corpse
                    && lootItem?.ItemOwner?.RootItem != null // Item exists
                    && !lootItem.ItemOwner.RootItem.QuestItem // Item is not a quest item
                    && _lootingBrain.IsValuableEnough(lootItem.ItemOwner.RootItem) // Item meets value threshold
                    && _lootingBrain.Stats.AvailableGridSpaces
                        > lootItem.ItemOwner.RootItem.GetItemSize() // Bot has enough space to pickup
                    && !_lootingBrain.IsLootIgnored(lootItem.ItemOwner.RootItem.Id); // Item not ignored

                bool canLootCorpse =
                    LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                        _botOwner.Profile.Info.Settings.Role
                    )
                    && corpse != null // Corpse exists
                    && corpse.GetPlayer != null // Corpse is a bot corpse and not a static "Dead scav" corpse
                    && !_lootingBrain.IsLootIgnored(corpse.name); // Corpse is not ignored

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
                        if (canLootContainer)
                        {
                            _lootingBrain.ActiveContainer = container;
                            _lootingBrain.LootObjectPosition = container.transform.position;
                            ActiveLootCache.CacheActiveLootId(container.Id, _botOwner.name);
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
                        else if (canLootCorpse)
                        {
                            _lootingBrain.ActiveCorpse = corpse;
                            _lootingBrain.LootObjectPosition = corpse.Transform.position;
                            ActiveLootCache.CacheActiveLootId(corpse.name, _botOwner.name);
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
                        else
                        {
                            _lootingBrain.ActiveItem = lootItem;
                            _lootingBrain.LootObjectPosition = lootItem.transform.position;
                            ActiveLootCache.CacheActiveLootId(
                                lootItem.ItemOwner.RootItem.Id,
                                _botOwner.name
                            );
                            _lootingBrain.DistanceToLoot = dist;
                            _lootingBrain.Destination = destination;
                            break;
                        }
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

            if (destination == Vector3.zero)
            {
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
            return NavMesh.SamplePosition(
                center - padding,
                out navMeshAlignedPoint,
                1f,
                navMeshAlignedPoint.mask
            )
                ? navMeshAlignedPoint.position
                : pointNearbyContainer;
        }
    }
}
