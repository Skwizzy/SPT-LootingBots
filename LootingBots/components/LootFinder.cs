using System;
using System.Collections;
using System.Collections.Generic;
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
        public List<string> IgnoredLootIds;

        // Container ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public List<string> NonNavigableContainerIds;

        // Booling showing when the looting task is running
        public bool IsLooting = false;

        // Amount of time in seconds to wait after looting successfully
        public float WaitAfterLootTimer;

        private static readonly float TimeToLoot = 8f;
        private BotLog _log;

        public void Init(BotOwner botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            BotOwner = botOwner;
            ItemAdder = new ItemAdder(BotOwner, this);
            IgnoredLootIds = new List<string> { };
            NonNavigableContainerIds = new List<string> { };
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

                    // Open any nearby door
                    BotOwner.DoorOpener.Update();
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
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
            }

            // Only ignore the corpse if looting was not interrupted
            CleanupCorpse(lootTask.Result);

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
            }

            // Only ignore the container if looting was not interrupted
            CleanupContainer(lootTask.Result);

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
            }

            // Need to manually cleanup item because the ItemOwner on the original object changes. Only ignore if looting was not interrupted
            CleanupItem(lootTask.Result, item);

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
            NonNavigableContainerIds.Add(lootId);
            Cleanup();
            IncrementLootTimer(30f);
        }

        public void IncrementLootTimer(float time = -1f)
        {
            // Increment loot wait timer
            float timer = time != -1f ? time : LootingBots.TimeToWaitBetweenLoot.Value + TimeToLoot;
            WaitAfterLootTimer = Time.time + timer;
        }

        public bool HasActiveLootable()
        {
            return ActiveContainer != null || ActiveItem != null || ActiveCorpse != null;
        }

        public void IgnoreLoot(string id)
        {
            IgnoredLootIds.Add(id);
        }

        public void EnableTransactions()
        {
            ItemAdder.EnableTransactions();
        }

        public void DisableTransactions()
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
