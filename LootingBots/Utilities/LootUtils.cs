using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;

using UnityEngine;

namespace LootingBots.Utilities
{
    public static class LootUtils
    {
        public static LayerMask LowPolyMask = LayerMask.GetMask(["LowPolyCollider"]);
        public static LayerMask LootMask = LayerMask.GetMask(["Interactive", "Loot", "Deadbody"]);
        public static int RESERVED_SLOT_COUNT = 2;

        private static readonly EquipmentSlot[] WeaponSlots = [
            EquipmentSlot.Holster,
            EquipmentSlot.FirstPrimaryWeapon,
            EquipmentSlot.SecondPrimaryWeapon
        ];

        private static readonly EquipmentSlot[] StorageSlots = [
            EquipmentSlot.Backpack,
            EquipmentSlot.ArmorVest,
            EquipmentSlot.TacticalVest,
            EquipmentSlot.Pockets
        ];

        private static readonly EquipmentSlot[] OtherSlots = [
            EquipmentSlot.Headwear,
            EquipmentSlot.Earpiece,
            EquipmentSlot.Dogtag,
            EquipmentSlot.Scabbard,
            EquipmentSlot.FaceCover
        ];

        /** Calculate the size of a container */
        public static int GetContainerSize(this SearchableItemItemClass container)
        {
            StashGridClass[] grids = container.Grids;
            int gridSize = 0;

            foreach (StashGridClass grid in grids)
            {
                gridSize += grid.GridHeight * grid.GridWidth;
            }

            return gridSize;
        }

        /// <summary>
        /// Checks if a key is a Single Use Item like the "Unknown Key"
        /// </summary>
        /// <param name="item">The item to check</param>
        /// <returns>returns true if it's single use, false otherwise</returns>
        public static bool IsSingleUseKey(this Item item)
        {
            KeyComponent key = item.GetItemComponent<KeyComponent>();
            return key != null && key.Template.MaximumNumberOfUsage == 1;
        }

        /** Triggers a container to open/close */
        public static void InteractContainer(LootableContainer container, EInteractionType action)
        {
            InteractionResult result = new InteractionResult(action);
            container?.Interact(result);
        }

        /**
        * Calculates the amount of empty grid slots in the container
        */
        public static int GetAvailableGridSlots(StashGridClass[] grids)
        {
            if (grids == null)
            {
                grids = [];
            }

            // Initialize freeSpaces to 0
            int freeSpaces = 0;

            // Loop through each grid and calculate the free spaces
            foreach (StashGridClass grid in grids)
            {
                int gridSize = grid.GridHeight * grid.GridWidth;
                int containedItemSize = grid.GetSizeOfContainedItems();
                freeSpaces += gridSize - containedItemSize;
            }

            return freeSpaces;
        }

        /// <summary>
        /// returns the amount of space taken up by all the items in a given grid slot
        /// </summary>
        /// <param name="grid">The grid to calculate the amount of space taken up for</param>
        /// <returns>Returns the item size as an integer</returns>
        public static int GetSizeOfContainedItems(this StashGridClass grid)
        {
            int containedItemSize = 0;

            // Loop through each item in grid.Items and accumulate the item size
            foreach (Item item in grid.Items)
            {
                containedItemSize += item.GetItemSize();
            }

            return containedItemSize;
        }

        /// <summary>
        /// Get the size of an item in a grid
        /// </summary>
        /// <param name="item">The item to get the size for</param>
        public static int GetItemSize(this Item item)
        {
            var dimensions = item.CalculateCellSize();
            return dimensions.X * dimensions.Y;
        }

        /** Given an item that is stackable and can be merged, search through the inventory and find any matches of that item that are not in a secure container. */
        public static Item FindItemToMerge(this InventoryController controller, Item item)
        {
            // Return null if item cannot be stacked
            if (item.StackMaxSize <= 1)
            {
                return null;
            }

            Item mergeTarget = null;

            // Use the item's template id to search for the same item in the inventory
            foreach (Item foundItem in controller.Inventory.GetAllItemByTemplate(item.TemplateId))
            {
                if (foundItem == null)
                {
                    continue;
                }

                Item rootItem = foundItem.GetRootItem();

                if (rootItem.Parent.Container.ID.Equals("securedcontainer", StringComparison.OrdinalIgnoreCase) == true)
                {
                    continue;
                }

                if (item.StackObjectsCount + foundItem.StackObjectsCount <= foundItem.StackMaxSize)
                {
                    mergeTarget = foundItem;
                    break; // Exit early when a valid merge target is found
                }
            }

            return mergeTarget;
        }

        /**
       *   Returns the list of slots to loot from a corpse in priority order. When a bot already has a backpack/rig, they will attempt to loot the weapons off the bot first. Otherwise they will loot the equipement first and loot the weapons afterwards.
       */
        public static IEnumerable<Slot> GetPrioritySlots(InventoryController targetInventory)
        {
            var equipment = targetInventory.Inventory.Equipment;
            bool hasBackpack = equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem != null;
            bool hasTacVest = equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem != null;

            var prioritySlots = new List<EquipmentSlot>(13);

            // Add slots in priority order
            if (hasBackpack || hasTacVest)
            {
                AddUnlockedSlots(equipment, prioritySlots, WeaponSlots);
                AddUnlockedSlots(equipment, prioritySlots, StorageSlots);
            }
            else
            {
                AddUnlockedSlots(equipment, prioritySlots, StorageSlots);
                AddUnlockedSlots(equipment, prioritySlots, WeaponSlots);
            }

            AddUnlockedSlots(equipment, prioritySlots, OtherSlots);

            return equipment.GetSlotsByName(prioritySlots);
        }

        private static void AddUnlockedSlots(InventoryEquipment equipment, List<EquipmentSlot> targetList, EquipmentSlot[] slots)
        {
            foreach (var slot in slots)
            {
                if (!equipment.GetSlot(slot).Locked)
                {
                    targetList.Add(slot);
                }
            }
        }

        /** Given a LootItemClass that has slots, return any items that are listed in slots flagged as "Locked" */
        public static IEnumerable<Item> GetAllLockedItems(CompoundItem itemWithSlots)
        {
            List<Item> resultItems = [];

            if (itemWithSlots.Slots == null)
            {
                return resultItems;
            }

            // Iterate over each slot in Slots
            foreach (Slot slot in itemWithSlots.Slots)
            {
                if (slot.Locked)
                {
                    // Add all items from the locked slot to the result list
                    if (slot.Items != null)
                    {
                        resultItems.AddRange(slot.Items);
                    }
                }
            }

            return resultItems;
        }
    }
}
