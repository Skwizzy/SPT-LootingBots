using System.Text;

using DrakiaXYZ.BigBrain.Brains;

using EFT;
using EFT.Interactive;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots.Brain.Logics
{
    internal class FindLootLogic : CustomLogic
    {
        private readonly LootingBrain _lootingBrain;
        private readonly BotLog _log;

        private float DetectCorpseDistance
        {
            get { return Mathf.Pow(LootingBots.DetectCorpseDistance.Value, 2); }
        }
        private float DetectContainerDistance
        {
            get { return Mathf.Pow(LootingBots.DetectContainerDistance.Value, 2); }
        }
        private float DetectItemDistance
        {
            get { return Mathf.Pow(LootingBots.DetectItemDistance.Value, 2); }
        }

        public FindLootLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            _lootingBrain = botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
        }

        public override void Update()
        {
            // If the bot has more than the reserved amount of slots needed for ammo, trigger a loot scan
            if (_lootingBrain.Stats.AvailableGridSpaces > LootUtils.RESERVED_SLOT_COUNT)
            {
                FindLootable();
            }
        }

        public void FindLootable()
        {
            LootableContainer closestContainer = null;
            LootItem closestItem = null;
            BotOwner closestCorpse = null;
            float shortestDist = -1f;

            // Use the largest detection radius specified in the settings as the main Sphere radius
            float detectionRadius = Mathf.Max(
                LootingBots.DetectItemDistance.Value,
                LootingBots.DetectContainerDistance.Value,
                LootingBots.DetectCorpseDistance.Value
            );

            // Cast a sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                BotOwner.Position,
                detectionRadius,
                LootUtils.LootMask,
                QueryTriggerInteraction.Collide
            );

            // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
            foreach (Collider collider in array)
            {
                LootableContainer container =
                    collider.gameObject.GetComponentInParent<LootableContainer>();
                LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                BotOwner corpse = collider.gameObject.GetComponentInParent<BotOwner>();

                bool canLootContainer =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && container != null // Container exists
                    && !_lootingBrain.IsLootIgnored(container.Id) // Container is not ignored
                    && container.isActiveAndEnabled // Container is marked as active and enabled
                    && container.DoorState != EDoorState.Locked; // Container is not locked

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
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
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && corpse != null // Corpse exists
                    && corpse.GetPlayer != null // Corpse is a bot corpse and not a static "Dead scav" corpse
                    && !_lootingBrain.IsLootIgnored(corpse.name); // Corpse is not ignored

                if (canLootContainer || canLootItem || canLootCorpse)
                {
                    // If we havent already visted the lootable, calculate its distance and save the lootable with the shortest distance
                    bool isInRange = IsLootInRange(container, lootItem, corpse, out float dist);

                    // If we are considering a lootable to be the new closest lootable, make sure the loot is in the detection range specified for the type of loot
                    if (isInRange && (shortestDist == -1f || dist < shortestDist))
                    {
                        if (canLootContainer)
                        {
                            closestItem = null;
                            closestCorpse = null;
                            closestContainer = container;
                        }
                        else if (canLootCorpse)
                        {
                            closestItem = null;
                            closestContainer = null;
                            closestCorpse = corpse;
                        }
                        else
                        {
                            closestCorpse = null;
                            closestContainer = null;
                            closestItem = lootItem;
                        }

                        shortestDist = dist;

                        _lootingBrain.LootObjectCenter = collider.bounds.center;
                        // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                        _lootingBrain.LootObjectCenter.y =
                            collider.bounds.center.y - collider.bounds.extents.y - 0.4f;
                    }
                }
            }

            if (closestContainer != null)
            {
                _lootingBrain.ActiveContainer = closestContainer;
                _lootingBrain.LootObjectPosition = closestContainer.transform.position;
                ActiveLootCache.CacheActiveLootId(closestContainer.Id, BotOwner.name);
            }
            else if (closestItem != null)
            {
                _lootingBrain.ActiveItem = closestItem;
                _lootingBrain.LootObjectPosition = closestItem.transform.position;
                ActiveLootCache.CacheActiveLootId(closestItem.ItemOwner.RootItem.Id, BotOwner.name);
            }
            else if (closestCorpse != null)
            {
                _lootingBrain.ActiveCorpse = closestCorpse;
                _lootingBrain.LootObjectPosition = closestCorpse.Transform.position;
                ActiveLootCache.CacheActiveLootId(closestCorpse.name, BotOwner.name);
            }

            _lootingBrain.DistanceToLoot = shortestDist;
        }

        /**
        * Checks to see if any of the found lootable items are within their detection range specified in the mod settings.
        */
        public bool IsLootInRange(
            LootableContainer container,
            LootItem lootItem,
            BotOwner corpse,
            out float dist
        )
        {
            Vector3 vector =
                BotOwner.Position
                - (
                    container?.transform.position
                    ?? lootItem?.transform.position
                    ?? corpse.GetPlayer.Transform.position
                );
            dist = vector.sqrMagnitude;

            return (container != null && DetectContainerDistance >= dist)
                || (lootItem != null && DetectItemDistance >= dist)
                || (corpse != null && DetectCorpseDistance >= dist);
        }
    }
}
