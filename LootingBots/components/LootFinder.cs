using System;
using System.Collections;
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

        // Collider.transform.position for the active lootable. Used in LOS checks to make sure bots dont loot through walls
        public Vector3 LootObjectPosition;

        // Container ids that the bot has looted
        public string[] IgnoredLootIds;

        // Container ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public string[] NonNavigableContainerIds;

        // Booling showing when the looting task is running
        public bool IsLooting = false;

        // Amount of time in seconds to wait after looting a container before finding the next container
        private float _waitAfterLootTimer;

        // // Amount of time to wait before clearning the nonNavigableContainerIds array
        // public float ClearNonNavigableIdTimer = 0f;
        private static readonly float TimeToLoot = 8f;
        private float _scanTimer;
        private BotLog _log;

        public void Init(BotOwner botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            BotOwner = botOwner;
            ItemAdder = new ItemAdder(BotOwner, this);
            IgnoredLootIds = new string[0];
            NonNavigableContainerIds = new string[0];
        }

        public async Task Update()
        {
            try
            {
                WildSpawnType botType = BotOwner.Profile.Info.Settings.Role;
                bool isLootFinderEnabled =
                    LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(botType)
                    || LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(botType)
                    || LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(botType);

                if (isLootFinderEnabled && BotOwner.BotState == EBotState.Active)
                {
                    if (ItemAdder.ShouldSort)
                    {
                        // Sort items in tacVest for better space management
                        await ItemAdder.SortTacVest();
                    }

                    BotOwner.DoorOpener.Update();

                    if (
                        _waitAfterLootTimer < Time.time
                        && _scanTimer < Time.time
                        && !HasActiveLootable()
                    )
                    {
                        _log.LogDebug($"Searching for nearby loot");
                        FindLootable();
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
        }

        public void FindLootable()
        {
            _scanTimer = Time.time + 6f;

            LootableContainer closestContainer = null;
            LootItem closestItem = null;
            BotOwner closestCorpse = null;
            float shortestDist = -1f;

            // Cast a 25m sphere on the bot, detecting any Interacive world objects that collide with the sphere
            Collider[] array = Physics.OverlapSphere(
                BotOwner.Position,
                LootingBots.DetectLootDistance.Value,
                LootUtils.LootMask,
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
                    && !IsLootIgnored(corpse.name);

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

                        LootObjectCenter = collider.bounds.center;
                        // Push the center point to the lowest y point in the collider. Extend it further down by .3f to help container positions of jackets snap to a valid NavMesh
                        LootObjectCenter.y =
                            collider.bounds.center.y - collider.bounds.extents.y - 0.4f;
                    }
                }
            }

            if (closestContainer != null)
            {
                _log.LogDebug($"Found container {closestContainer.name.Localized()}");
                ActiveContainer = closestContainer;
                LootObjectPosition = closestContainer.transform.position;

                ActiveLootCache.CacheActiveLootId(closestContainer.Id, BotOwner.name);
            }
            else if (closestItem != null)
            {
                _log.LogDebug(
                    $"Found item {closestItem.Name.Localized()} {closestItem.ItemOwner.RootItem.Id}"
                );

                ActiveItem = closestItem;
                LootObjectPosition = closestItem.transform.position;

                ActiveLootCache.CacheActiveLootId(closestItem.ItemOwner.RootItem.Id, BotOwner.name);
            }
            else if (closestCorpse != null)
            {
                _log.LogDebug($"Found corpse: {closestCorpse.name}");
                ActiveCorpse = closestCorpse;
                LootObjectPosition = closestCorpse.Transform.position;

                ActiveLootCache.CacheActiveLootId(closestCorpse.name, BotOwner.name);
            }
        }

        public void StartLooting()
        {
            if (ActiveContainer)
            {
                StartCoroutine(LootContainer());
            }
            else if (ActiveItem)
            {
                StartCoroutine(LootItem());
            }
            else if (ActiveCorpse)
            {
                StartCoroutine(LootCorpse());
            }
        }

        // Logic to handle the looting of corpses
        public IEnumerator LootCorpse()
        {
            IsLooting = true;
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

            Task<bool> lootTask = ItemAdder.TryAddItemsToBot(priorityItems);
            yield return new WaitUntil(() => lootTask.IsCompleted);

            ItemAdder.UpdateActiveWeapon();
            IsLooting = false;

            if (lootTask.Result)
            {
                IncrementLootTimer();
                CleanupCorpse();
            }
            watch.Stop();
            _log.LogDebug($"Corpse loot time: {watch.ElapsedMilliseconds / 1000f}s");
        }

        // Logic to handle the looting of containers
        public IEnumerator LootContainer()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            IsLooting = true;

            Item item = ActiveContainer.ItemOwner.Items.ToArray()[0];
            _log.LogDebug($"Trying to add items from: {item.Name.Localized()}");

            bool didOpen = false;
            // If a container was closed, open it before looting
            if (ActiveContainer.DoorState == EDoorState.Shut)
            {
                LootUtils.InteractContainer(ActiveContainer, EInteractionType.Open);
                didOpen = true;
            }

            Task delayTask = TransactionController.SimulatePlayerDelay(2000);
            yield return new WaitUntil(() => delayTask.IsCompleted);

            Task<bool> lootTask = ItemAdder.LootNestedItems(item);
            yield return new WaitUntil(() => lootTask.IsCompleted);

            // Close the container after looting if a container was open, and the bot didnt open it
            if (ActiveContainer.DoorState == EDoorState.Open && !didOpen)
            {
                LootUtils.InteractContainer(ActiveContainer, EInteractionType.Close);
            }

            ItemAdder.UpdateActiveWeapon();
            IsLooting = false;

            if (lootTask.Result)
            {
                IncrementLootTimer();
                CleanupContainer();
            }

            watch.Stop();
            _log.LogDebug($"Container loot time: {watch.ElapsedMilliseconds / 1000f}s");
        }

        public IEnumerator LootItem()
        {
            IsLooting = true;

            Item item = ActiveItem.ItemOwner.RootItem;

            _log.LogDebug($"Trying to pick up loose item: {item.Name.Localized()}");
            BotOwner.GetPlayer.UpdateInteractionCast();
            Task<bool> lootTask = ItemAdder.TryAddItemsToBot(new Item[] { item });

            yield return new WaitUntil(() => lootTask.IsCompleted);

            BotOwner.GetPlayer.CurrentState.Pickup(false, null);
            ItemAdder.UpdateActiveWeapon();

            if (lootTask.Result)
            {
                IncrementLootTimer();
                CleanupItem(true, item);
            }

            IsLooting = false;
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
                ActiveContainer?.Id ?? ActiveItem?.ItemOwner.RootItem.Id ?? ActiveCorpse.name;
            NonNavigableContainerIds.Append(lootId);
            Cleanup();
            IncrementLootTimer(30f);
        }

        public void IncrementLootTimer(float time = -1f)
        {
            // Increment loot wait timer
            float timer = time != -1f ? time : LootingBots.TimeToWaitBetweenLoot.Value + TimeToLoot;
            _waitAfterLootTimer = Time.time + timer;
        }

        public bool HasActiveLootable()
        {
            return ActiveContainer != null || ActiveItem != null || ActiveCorpse != null;
        }

        public void IgnoreLoot(string id)
        {
            IgnoredLootIds.Append(id);
        }

        public void Resume()
        {
            ItemAdder.EnableTransactions();
        }

        public void Pause()
        {
            ItemAdder.DisableTransactions();
        }

        // Removes all active lootables from LootFinder and cleans them from the cache
        public void Cleanup(bool ignore = true)
        {
            if (ActiveContainer != null)
            {
                CleanupContainer(ignore);
            }

            if (ActiveItem != null)
            {
                CleanupItem(ignore);
            }

            if (ActiveCorpse != null)
            {
                CleanupCorpse(ignore);
            }
        }

        public void CleanupContainer(bool ignore = true)
        {
            LootableContainer container = ActiveContainer;
            _log.LogWarning(
                $"Clearing active container: {container.name.Localized()} ({container.Id})"
            );
            ActiveLootCache.Cleanup(container.Id);

            if (ignore)
            {
                IgnoreLoot(container.Id);
            }

            ActiveContainer = null;
        }

        public void CleanupItem(bool ignore = true, Item movedItem = null)
        {
            Item item = movedItem ?? ActiveItem.ItemOwner?.RootItem;

            _log.LogWarning($"Clearing active item: {item?.Name?.Localized()} ({item?.Id})");
            ActiveLootCache.Cleanup(item.Id);

            if (ignore)
            {
                IgnoreLoot(item.Id);
            }

            ActiveItem = null;
        }

        public void CleanupCorpse(bool ignore = true)
        {
            BotOwner corpse = ActiveCorpse;
            string name = corpse.name;
            _log.LogWarning(
                $"Clearing active corpse: {name} ({corpse.GetPlayer.name.Localized()})"
            );
            ActiveLootCache.Cleanup(name);

            if (ignore)
            {
                IgnoreLoot(name);
            }

            ActiveCorpse = null;
        }
    }
}
