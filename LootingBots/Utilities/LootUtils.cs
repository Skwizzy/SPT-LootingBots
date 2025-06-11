using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;

using UnityEngine;

namespace LootingBots.Utilities
{
    public static class LootUtils
    {
        public static LayerMask LowPolyMask = LayerMask.GetMask(["LowPolyCollider"]);
        public static LayerMask LootMask = LayerMask.GetMask(
            ["Interactive", "Loot", "Deadbody"]
        );
        public static int RESERVED_SLOT_COUNT = 2;

        public static InventoryController GetBotInventoryController(Player targetBot)
        {
            return targetBot.InventoryController;
        }

        /** Calculate the size of a container */
        public static int GetContainerSize(SearchableItemItemClass container)
        {
            StashGridClass[] grids = container.Grids;
            int gridSize = 0;

            foreach (StashGridClass grid in grids)
            {
                gridSize += grid.GridHeight * grid.GridWidth;
            }

            return gridSize;
        }

        // Prevents bots from looting single use quest keys like "Unknown Key"
        public static bool IsSingleUseKey(Item item)
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

        /** Return the amount of spaces taken up by all the items in a given grid slot */
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

        /** Gets the size of an item in a grid */
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
            bool hasBackpack = targetInventory.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem != null;
            bool hasTacVest = targetInventory.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem != null;

            IEnumerable<EquipmentSlot> prioritySlots = [];
            IEnumerable<EquipmentSlot> weaponSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                [
                    EquipmentSlot.Holster,
                    EquipmentSlot.FirstPrimaryWeapon,
                    EquipmentSlot.SecondPrimaryWeapon
                ]
            );
            IEnumerable<EquipmentSlot> storageSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                [
                    EquipmentSlot.Backpack,
                    EquipmentSlot.ArmorVest,
                    EquipmentSlot.TacticalVest,
                    EquipmentSlot.Pockets
                ]
            );

            IEnumerable<EquipmentSlot> otherSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                [
                    EquipmentSlot.Headwear,
                    EquipmentSlot.Earpiece,
                    EquipmentSlot.Dogtag,
                    EquipmentSlot.Scabbard,
                    EquipmentSlot.FaceCover
                ]
            );

            prioritySlots = hasBackpack || hasTacVest
                ? prioritySlots.Concat(weaponSlots).Concat(storageSlots).ToArray()
                : prioritySlots.Concat(storageSlots).Concat(weaponSlots).ToArray();

            prioritySlots = prioritySlots.Concat(otherSlots).ToArray();

            return targetInventory.Inventory.Equipment.GetSlotsByName(prioritySlots);
        }

        /** Given a list of slots, return all slots that are not flagged as Locked */
        private static IEnumerable<EquipmentSlot> GetUnlockedEquipmentSlots(InventoryController targetInventory, EquipmentSlot[] desiredSlots)
        {
            List<EquipmentSlot> unlockedSlots = new();

            // Loop through each desired slot
            foreach (EquipmentSlot slot in desiredSlots)
            {
                // Check if the slot is unlocked
                if (targetInventory.Inventory.Equipment.GetSlot(slot).Locked == false)
                {
                    // Add the unlocked slot to the result list
                    unlockedSlots.Add(slot);
                }
            }

            return unlockedSlots;
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