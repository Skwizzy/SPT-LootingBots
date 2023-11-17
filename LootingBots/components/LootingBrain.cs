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

    public class LootingBrain : MonoBehaviour
    {
        public BotOwner BotOwner;

        // Component responsible for adding items to the bot inventory
        public InventoryController InventoryController;

        // Current container that the bot will try to loot
        public LootableContainer ActiveContainer;

        // Current loose item that the bot will try to loot
        public LootItem ActiveItem;

        // Current corpse that the bot will try to loot
        public BotOwner ActiveCorpse;

        // Final destination of the bot when moving to loot something
        public Vector3 Destination;

        // Collider.transform.position for the active lootable. Used in LOS checks to make sure bots dont loot through walls
        public Vector3 LootObjectPosition;

        // Object ids that the bot has looted
        public List<string> IgnoredLootIds;

        // Object ids that were not able to be reached even though a valid path exists. Is cleared every 2 mins by default
        public List<string> NonNavigableLootIds;

        public bool IsBrainEnabled
        {
            get
            {
                return LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    || LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    )
                    || LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                        BotOwner.Profile.Info.Settings.Role
                    );
            }
        }

        public BotStats Stats
        {
            get { return InventoryController.Stats; }
        }

        public bool HasActiveLootable
        {
            get { return ActiveContainer != null || ActiveItem != null || ActiveCorpse != null; }
        }

        public bool IsBotLooting
        {
            get { return LootTaskRunning || HasActiveLootable; }
        }

        // Boolean showing when the looting coroutine is running
        public bool LootTaskRunning = false;

        public float DistanceToLoot = -1f;

        private BotLog _log;

        private const int LootingStartDelay = 3000;

        public void Init(BotOwner botOwner)
        {
            _log = new BotLog(LootingBots.LootLog, botOwner);
            BotOwner = botOwner;
            InventoryController = new InventoryController(BotOwner, this);
            IgnoredLootIds = new List<string> { };
            NonNavigableLootIds = new List<string> { };
        }

        /*
        * LootFinder update should only be running if one of the looting settings is enabled and the bot is in an active state
        */
        public async Task Update()
        {
            try
            {
                if (IsBrainEnabled && BotOwner.BotState == EBotState.Active)
                {
                    if (InventoryController.ShouldSort)
                    {
                        // Sort items in tacVest for better space management
                        await InventoryController.SortTacVest();
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

        /**
        * Determines the looting action to take depending on the current Active object in the LootFinder. There can only be one Active object at a time
        */
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

        /**
        * Handles looting a corpse found on the map.
        */
        public IEnumerator LootCorpse()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            LootTaskRunning = true;
            _log.LogWarning($"Trying to loot corpse");

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

            // Get items to loot from the corpse in a priority order based off the slots
            EquipmentSlot[] prioritySlots = InventoryController.GetPrioritySlots();

            Item[] priorityItems = corpseInventoryController.Inventory.Equipment
                .GetSlotsByName(prioritySlots)
                .Select(slot => slot.ContainedItem)
                .Where(item => item != null && !item.IsUnremovable)
                .ToArray();

            Task delayTask = TransactionController.SimulatePlayerDelay(LootingStartDelay);
            yield return new WaitUntil(() => delayTask.IsCompleted);

            Task<bool> lootTask = InventoryController.TryAddItemsToBot(priorityItems);
            yield return new WaitUntil(() => lootTask.IsCompleted);

            InventoryController.UpdateActiveWeapon();

            // Only ignore the corpse if looting was not interrupted
            CleanupCorpse(lootTask.Result);
            OnLootTaskEnd(lootTask.Result);

            watch.Stop();
            _log.LogDebug(
                $"Corpse loot time: {watch.ElapsedMilliseconds / 1000f}s. Net Worth: {Stats.NetLootValue}"
            );
        }

        /**
        * Handles looting a container found on the map.
        */
        public IEnumerator LootContainer()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            LootTaskRunning = true;

            Item item = ActiveContainer.ItemOwner.Items.ToArray()[0];
            _log.LogDebug($"Trying to add items from: {item.Name.Localized()}");

            bool didOpen = false;
            // If a container was closed, open it before looting
            if (ActiveContainer?.DoorState == EDoorState.Shut)
            {
                LootUtils.InteractContainer(ActiveContainer, EInteractionType.Open);
                didOpen = true;
            }

            Task delayTask = TransactionController.SimulatePlayerDelay(LootingStartDelay);
            yield return new WaitUntil(() => delayTask.IsCompleted);

            Task<bool> lootTask = InventoryController.LootNestedItems(item);
            yield return new WaitUntil(() => lootTask.IsCompleted);

            // Close the container after looting if a container was open, and the bot didnt open it
            if (ActiveContainer?.DoorState == EDoorState.Open && !didOpen)
            {
                LootUtils.InteractContainer(ActiveContainer, EInteractionType.Close);
            }

            InventoryController.UpdateActiveWeapon();

            // Only ignore the container if looting was not interrupted
            CleanupContainer(lootTask.Result);
            OnLootTaskEnd(lootTask.Result);

            watch.Stop();
            _log.LogDebug(
                $"Container loot time: {watch.ElapsedMilliseconds / 1000f}s. Net Worth: {Stats.NetLootValue}"
            );
        }

        /**
        * Handles looting a loose item found on the map.
        */
        public IEnumerator LootItem()
        {
            LootTaskRunning = true;

            Item item = ActiveItem.ItemOwner.RootItem;

            _log.LogDebug($"Trying to pick up loose item: {item.Name.Localized()}");
            BotOwner.GetPlayer.UpdateInteractionCast();
            Task<bool> lootTask = InventoryController.TryAddItemsToBot(new Item[] { item });

            yield return new WaitUntil(() => lootTask.IsCompleted);

            BotOwner.GetPlayer.CurrentManagedState.Pickup(false, null);
            InventoryController.UpdateActiveWeapon();

            // Need to manually cleanup item because the ItemOwner on the original object changes. Only ignore if looting was not interrupted
            CleanupItem(lootTask.Result, item);
            OnLootTaskEnd(lootTask.Result);
            _log.LogDebug($"Net Worth: {Stats.NetLootValue}");
        }

        public void OnLootTaskEnd(bool lootingSuccessful)
        {
            UpdateGridStats();
            BotOwner.AIData.CalcPower();
            LootTaskRunning = false;
        }

        public void UpdateGridStats()
        {
            InventoryController.UpdateGridStats();
        }

        /**
        *  Check to see if the object being looted has been ignored due to bad navigation, being looted already, or if its in use by another bot
        */
        public bool IsLootIgnored(string lootId)
        {
            bool alreadyTried =
                NonNavigableLootIds.Contains(lootId) || IgnoredLootIds.Contains(lootId);

            return alreadyTried || ActiveLootCache.IsLootInUse(lootId);
        }

        /** Check if the item being looted meets the loot value threshold specified in the mod settings. PMC bots use the PMC loot threshold, all other bots such as scavs, bosses, and raiders will use the scav threshold */
        public bool IsValuableEnough(Item lootItem)
        {
            float itemValue = LootingBots.ItemAppraiser.GetItemPrice(lootItem);
            return InventoryController.IsValuableEnough(itemValue);
        }

        /**
        *  Handles adding non navigable loot to the list of non-navigable ids for use in the ignore logic. Additionaly removes the object from the active loot cache
        */
        public void HandleNonNavigableLoot()
        {
            string lootId =
                ActiveContainer?.Id ?? ActiveItem?.ItemOwner.RootItem.Id ?? ActiveCorpse.name;
            NonNavigableLootIds.Add(lootId);
            Cleanup();
        }


        /**
        * Adds a loot id to the list of loot items to ignore for a specific bot
        */
        public void IgnoreLoot(string id)
        {
            IgnoredLootIds.Add(id);
        }

        /**
        * Wrapper function to enable transactions to be executed by the InventoryController.
        */
        public void EnableTransactions()
        {
            InventoryController.EnableTransactions();
        }

        /**
        * Wrapper function to disable the execution of transactions by the InventoryController.
        */
        public void DisableTransactions()
        {
            InventoryController.DisableTransactions();
            Cleanup(false);
        }

        /**
        * Removes all active lootables from LootFinder and cleans them from the active loot cache
        */
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

        /**
        * Removes the ActiveContainer from the LootFinder and ActiveLootCache. Can optionally add the container to the ignore list after cleaning
        */
        public void CleanupContainer(bool ignore = true)
        {
            if (ActiveContainer != null)
            {
                LootableContainer container = ActiveContainer;
                ActiveLootCache.Cleanup(container.Id);

                if (ignore)
                {
                    IgnoreLoot(container.Id);
                }

                ActiveContainer = null;
            }
        }

        /**
        * Removes the ActiveItem from the LootFinder and ActiveLootCache. Can optionally add the item to the ignore list after cleaning
        */
        public void CleanupItem(bool ignore = true, Item movedItem = null)
        {
            Item item = movedItem ?? ActiveItem.ItemOwner?.RootItem;
            if (item != null)
            {
                ActiveLootCache.Cleanup(item.Id);

                if (ignore)
                {
                    IgnoreLoot(item.Id);
                }

                ActiveItem = null;
            }
        }

        /**
        * Removes the ActiveCorpse from the LootFinder and ActiveLootCache. Can optionally add the corpse to the ignore list after cleaning
        */
        public void CleanupCorpse(bool ignore = true)
        {
            if (ActiveCorpse != null)
            {
                BotOwner corpse = ActiveCorpse;
                string name = corpse.name;
                ActiveLootCache.Cleanup(name);

                if (ignore)
                {
                    IgnoreLoot(name);
                }

                ActiveCorpse = null;
            }
        }
    }
}
