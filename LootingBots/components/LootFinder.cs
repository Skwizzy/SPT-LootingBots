using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots.Patch.Components
{
    // Degug spheres from DrakiaXYZ Waypoints https://github.com/DrakiaXYZ/SPT-Waypoints/blob/master/Helpers/GameObjectHelper.cs
    public class GameObjectHelper
    {
        public static GameObject DrawSphere(Vector3 position, float size, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponent<Renderer>().material.color = color;
            sphere.GetComponent<Collider>().enabled = false;
            sphere.transform.position = new Vector3(position.x, position.y, position.z);
            sphere.transform.localScale = new Vector3(size, size, size);

            return sphere;
        }
    }

    public class LootFinder : MonoBehaviour
    {
        public BotOwner BotOwner;
        public ItemAdder ItemAdder;

        // Current container that the bot will try to loot
        public LootableContainer ActiveContainer;

        // Current loose item that the bot will try to loot
        public LootItem ActiveItem;

        // Current corpse that the bot will try to loot
        public BotOwner ActiveCorpse;

        // Center of the container's collider used to help in navigation
        public Vector3 LootObjectCenter;

        // Container ids that the bot has looted
        public string[] IgnoredLootIds = new string[] { };

        // Container ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public string[] NonNavigableContainerIds = new string[] { };

        // Amount of time in seconds to wait after looting a container before finding the next container
        public float WaitAfterLooting = 0f;

        // Amount of time to wait before clearning the nonNavigableContainerIds array
        public float ClearNonNavigableIdTimer = 0f;
        public static float TimeToLoot = 8f;
        private bool _paused = false;
        private float _scanTimer = 0f;
        private Log _log;

        public void Init(BotOwner botOwner)
        {
            _log = LootingBots.LootLog;
            BotOwner = botOwner;
            ItemAdder = new ItemAdder(BotOwner, this);
        }

        public async Task Update()
        {
            if (BotOwner == null)
            {
                _log.LogError("No BotOwner!");
            }

            try
            {
                WildSpawnType botType = BotOwner.Profile.Info.Settings.Role;
                bool isLootFinderEnabled =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(botType)
                    || LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(botType)
                    || LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(botType);

                if (ItemAdder.ShouldSort)
                {
                    // Sort items in tacVest for better space management
                    await ItemAdder.SortTacVest();
                }

                if (BotOwner.BotState == EBotState.Active)
                {
                    BotOwner.DoorOpener.Update();

                    if (
                        isLootFinderEnabled
                        && WaitAfterLooting < Time.time
                        && _scanTimer < Time.time
                        && !HasActiveLootable()
                    )
                    {
                        _log.LogDebug($"Bot {BotOwner.Id} searching for nearby loot");
                        FindLootable();
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Bot {BotOwner.Id} {e}");
            }
        }

        public void FindLootable()
        {
            _scanTimer = Time.time + 6f;

            LootableContainer closestContainer = null;
            LootItem closestItem = null;
            BotOwner closestCorpse = null;
            Vector3 closestLootableCenter = new Vector3();
            float shortestDist = -1f;

            // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                BotOwner.Position,
                LootingBots.DetectLootDistance.Value,
                LayerMask.GetMask(new string[] { "Interactive", "Loot", "Deadbody" }),
                QueryTriggerInteraction.Collide
            );

            // For each object detected, check to see if it is a lootable container and then calculate its distance from the player
            foreach (Collider collider in array)
            {
                LootableContainer containerObj =
                    collider.gameObject.GetComponentInParent<LootableContainer>();
                LootItem lootItem = collider.gameObject.GetComponentInParent<LootItem>();
                BotOwner corpse = collider.gameObject.GetComponentInParent<BotOwner>();

                bool canLootContainer =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && containerObj != null
                    && !IsLootIgnored(containerObj.Id)
                    && containerObj.isActiveAndEnabled
                    && containerObj.DoorState != EDoorState.Locked;

                bool canLootItem =
                    LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && lootItem != null
                    && !(lootItem is Corpse)
                    && lootItem?.ItemOwner?.RootItem != null
                    && !lootItem.ItemOwner.RootItem.QuestItem
                    && !IsLootIgnored(lootItem.ItemOwner.RootItem.Id);

                bool canLootCorpse =
                    LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    && corpse != null
                    && !IsLootIgnored(corpse.Id.ToString());

                if (canLootContainer || canLootItem || canLootCorpse)
                {
                    // If we havent already visted the container, calculate its distance and save the container with the smallest distance
                    Vector3 vector =
                        BotOwner.Position
                        - (
                            containerObj?.transform.position
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
                            closestContainer = containerObj;
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

                        closestLootableCenter = collider.bounds.center;
                        // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                        closestLootableCenter.y =
                            collider.bounds.center.y - collider.bounds.extents.y - 0.4f;
                    }
                }
            }

            if (closestContainer != null)
            {
                _log.LogDebug($"Bot {BotOwner.Id} found container");
                ActiveContainer = closestContainer;
                LootObjectCenter = closestLootableCenter;

                ActiveLootCache.CacheActiveLootId(closestContainer.Id, BotOwner.Id);
            }
            else if (closestItem != null)
            {
                _log.LogDebug(
                    $"Bot {BotOwner.Id} found item {closestItem.Name.Localized()} {closestItem.ItemOwner.RootItem.Id}"
                );

                ActiveItem = closestItem;
                LootObjectCenter = closestLootableCenter;

                ActiveLootCache.CacheActiveLootId(closestItem.ItemOwner.RootItem.Id, BotOwner.Id);
            }
            else if (closestCorpse != null)
            {
                _log.LogError($"Bot {BotOwner.Id} found corpse");
                ActiveCorpse = closestCorpse;
                LootObjectCenter = closestLootableCenter;

                ActiveLootCache.CacheActiveLootId(closestCorpse.Id.ToString(), BotOwner.Id);
            }
        }

        // Logic to handle the looting of corpses
        public async Task LootCorpse()
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                // Initialize corpse inventory controller
                Player corpsePlayer = ActiveCorpse.GetPlayer;
                Type corpseType = corpsePlayer.GetType();
                FieldInfo corpseInventory = corpseType.BaseType.GetField(
                    "_inventoryController",
                    BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.Public
                        | BindingFlags.Instance
                );
                InventoryControllerClass corpseInventoryController = (InventoryControllerClass)
                    corpseInventory.GetValue(corpsePlayer);

                EquipmentSlot[] prioritySlots = ItemAdder.GetPrioritySlots();
                _log.LogWarning($"Trying to loot corpse");

                Item[] priorityItems = corpseInventoryController.Inventory.Equipment
                    .GetSlotsByName(prioritySlots)
                    .Select(slot => slot.ContainedItem)
                    .Where(item => item != null && !item.IsUnremovable)
                    .ToArray();

                if (await ItemAdder.TryAddItemsToBot(priorityItems))
                {
                    IncrementLootTimer();
                }

                ItemAdder.UpdateActiveWeapon();

                watch.Stop();
                _log.LogDebug($"Corpse loot time: {watch.ElapsedMilliseconds / 1000f}s");
            }
            catch (Exception e)
            {
                _log.LogError($"{BotOwner.Id} {e}");
            }
        }

        // Logic to handle the looting of containers
        public async Task LootContainer(LootableContainer container)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            Item item = container.ItemOwner.Items.ToArray()[0];
            _log.LogDebug($"Bot {BotOwner.Id} trying to add items from: {item.Name.Localized()}");

            bool didOpen = false;
            // If a container was closed, open it before looting
            if (container.DoorState == EDoorState.Shut)
            {
                LootUtils.InteractContainer(container, EInteractionType.Open);
                didOpen = true;
            }

            await TransactionController.SimulatePlayerDelay(1000);

            if (await ItemAdder.LootNestedItems(item))
            {
                IncrementLootTimer();
            }

            // Close the container after looting if a container was open, and the bot didnt open it
            if (container.DoorState == EDoorState.Open && !didOpen)
            {
                LootUtils.InteractContainer(container, EInteractionType.Close);
            }

            ItemAdder.UpdateActiveWeapon();

            watch.Stop();
            _log.LogDebug($"Container loot time: {watch.ElapsedMilliseconds / 1000f}s");
        }

        public async Task LootItem()
        {
            Item item = ActiveItem.ItemOwner.RootItem;

            _log.LogDebug(
                $"Bot {BotOwner.Id} trying to pick up loose item: {item.Name.Localized()}"
            );
            BotOwner.GetPlayer.UpdateInteractionCast();

            if (await ItemAdder.TryAddItemsToBot(new Item[] { item }))
            {
                IncrementLootTimer();
            }

            BotOwner.GetPlayer.CurrentState.Pickup(false, null);
            ItemAdder.UpdateActiveWeapon();
        }

        public bool IsLootIgnored(string lootId)
        {
            bool alreadyTried =
                NonNavigableContainerIds.Contains(lootId) || IgnoredLootIds.Contains(lootId);

            return alreadyTried || ActiveLootCache.IsLootInUse(lootId);
        }

        public void HandleNonNavigableLoot()
        {
            string lootId =
                ActiveContainer?.Id
                ?? ActiveItem?.ItemOwner.RootItem.Id
                ?? ActiveCorpse.Id.ToString();
            NonNavigableContainerIds.Append(lootId);
            Cleanup();
            IncrementLootTimer(30f);
            // BotOwner.PatrollingData.MoveUpdate();
        }

        public void IncrementLootTimer(float time = -1f)
        {
            // Increment loot wait timer
            float timer = time != -1f ? time : LootingBots.TimeToWaitBetweenLoot.Value + TimeToLoot;
            WaitAfterLooting = Time.time + timer;
        }

        public bool HasActiveLootable()
        {
            return ActiveContainer != null || ActiveItem != null || ActiveCorpse != null;
        }

        public void IgnoreLoot(string id)
        {
            IgnoredLootIds.Append(id);
        }

        public bool IsPaused()
        {
            return _paused;
        }

        public void Resume()
        {
            ItemAdder.EnableTransactions();
            _paused = false;
        }

        public void Pause()
        {
            ItemAdder.DisableTransactions();
            _paused = true;
        }

        public void Cleanup()
        {
            if (ActiveContainer != null)
            {
                CleanupContainer();
            }

            if (ActiveItem != null)
            {
                CleanupItem();
            }

            if (ActiveCorpse != null)
            {
                CleanupCorpse();
            }
        }

        public void CleanupContainer()
        {
            LootableContainer container = ActiveContainer;
            ActiveContainer = null;
            IgnoredLootIds.Append(container.Id);
            ActiveLootCache.Cleanup(container.Id);
            LootingBots.LootLog.LogWarning(
                $"Removing container: {container.name.Localized()} ({container.Id})"
            );
        }

        public void CleanupItem()
        {
            LootItem item = ActiveItem;
            IgnoredLootIds.Append(item.ItemOwner.RootItem.Id);
            ActiveLootCache.Cleanup(item.ItemOwner.RootItem.Id);
            ActiveItem = null;

            LootingBots.LootLog.LogWarning(
                $"Removing item: {item.Name.Localized()} ({item.ItemOwner.RootItem.Id})"
            );
        }

        public void CleanupCorpse()
        {
            BotOwner corpse = ActiveCorpse;
            string corpseId = corpse.Id.ToString();
            IgnoredLootIds.Append(corpseId);
            ActiveLootCache.Cleanup(corpseId);
            ActiveCorpse = null;

            LootingBots.LootLog.LogWarning(
                $"Removing corpse: Bot {corpseId} ({corpse.GetPlayer.name.Localized()})"
            );
        }

        public void Destroy()
        {
            Destroy(this);
        }
    }
}
