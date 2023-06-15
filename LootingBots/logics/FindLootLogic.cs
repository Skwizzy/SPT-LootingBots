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
        private readonly LootFinder _lootFinder;
        private readonly BotLog _log;

        public FindLootLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            _lootFinder = botOwner.GetPlayer.gameObject.GetComponent<LootFinder>();
        }

        public override void Update()
        {
            // Kick off looting logic
            if (!_lootFinder.HasActiveLootable())
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

            // Cast a sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                BotOwner.Position,
                LootingBots.DetectLootDistance.Value,
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
                    && container != null
                    && !_lootFinder.IsLootIgnored(container.Id)
                    && container.isActiveAndEnabled
                    && container.DoorState != EDoorState.Locked;

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && lootItem != null
                    && !(lootItem is Corpse)
                    && lootItem?.ItemOwner?.RootItem != null
                    && !lootItem.ItemOwner.RootItem.QuestItem
                    && !_lootFinder.IsLootIgnored(lootItem.ItemOwner.RootItem.Id);

                bool canLootCorpse =
                    LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && corpse != null
                    && !_lootFinder.IsLootIgnored(corpse.name);

                if (canLootContainer || canLootItem || canLootCorpse)
                {
                    // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                    Vector3 vector =
                        BotOwner.Position
                        - (
                            container?.transform.position
                            ?? lootItem?.transform.position
                            ?? corpse.GetPlayer.Transform.position
                        );
                    float dist = vector.sqrMagnitude;

                    // If we are considering a container to be the new closest container, make sure the bot has a valid NavMeshPath for the container before adding it as the closest container
                    if (shortestDist == -1f || dist < shortestDist)
                    {
                        shortestDist = dist;

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

                        _lootFinder.LootObjectCenter = collider.bounds.center;
                        // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                        _lootFinder.LootObjectCenter.y =
                            collider.bounds.center.y - collider.bounds.extents.y - 0.4f;
                    }
                }
            }

            if (closestContainer != null)
            {
                _log.LogDebug($"Found container {closestContainer.name.Localized()}");
                _lootFinder.ActiveContainer = closestContainer;
                _lootFinder.LootObjectPosition = closestContainer.transform.position;

                ActiveLootCache.CacheActiveLootId(closestContainer.Id, BotOwner.name);
            }
            else if (closestItem != null)
            {
                _log.LogDebug(
                    $"Found item {closestItem.Name.Localized()} {closestItem.ItemOwner.RootItem.Id}"
                );

                _lootFinder.ActiveItem = closestItem;
                _lootFinder.LootObjectPosition = closestItem.transform.position;

                ActiveLootCache.CacheActiveLootId(closestItem.ItemOwner.RootItem.Id, BotOwner.name);
            }
            else if (closestCorpse != null)
            {
                _log.LogDebug($"Found corpse: {closestCorpse.name}");
                _lootFinder.ActiveCorpse = closestCorpse;
                _lootFinder.LootObjectPosition = closestCorpse.Transform.position;

                ActiveLootCache.CacheActiveLootId(closestCorpse.name, BotOwner.name);
            }
        }
    }
}