using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;

// Found in InteractionHsHandlerClass.Sort
using GridClassEx = GClass2516;
// Found in GClass2502.SetSearched
using GridCacheClass = GClass1401;
using GridManagerClass = GClass2824;
using SortResultStruct = GStruct414<GClass2824>;
using GridItemClass = GClass2521;
using ItemAddressExClass = ItemAddressClass;
using SortErrorClass = GClass3326;

namespace LootingBots.Patch.Util
{
    public static class LootUtils
    {
        public static LayerMask LowPolyMask = LayerMask.GetMask(new string[] { "LowPolyCollider" });
        public static LayerMask LootMask = LayerMask.GetMask(
            new string[] { "Interactive", "Loot", "Deadbody" }
        );
        public static int RESERVED_SLOT_COUNT = 2;

        public static InventoryControllerClass GetBotInventoryController(Player targetBot)
        {
            Type targetBotType = targetBot.GetType();
            FieldInfo botInventory = targetBotType.BaseType.GetField(
                "_inventoryController",
                BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.Instance
            );
            return (InventoryControllerClass)botInventory.GetValue(targetBot);
        }

        /** Calculate the size of a container */
        public static int GetContainerSize(SearchableItemClass container)
        {
            StashGridClass[] grids = container.Grids;
            int gridSize = 0;

            foreach (StashGridClass grid in grids)
            {
                gridSize += grid.GridHeight.Value * grid.GridWidth.Value;
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
        * Reference fn: InteractionHsHandlerClass.Sort
        * Sorts the items in a container and places them in grid spaces that match their exact size before moving on to a bigger slot size. This helps make more room in the container for items to be placed in
        */
        public static SortResultStruct SortContainer(
            SearchableItemClass container,
            InventoryControllerClass controller
        )
        {
            if (container != null)
            {
                List<object> newLocations = new List<object>();
                GridManagerClass gridManager = new GridManagerClass(container, controller);
                List<Item> itemsInContainer = new List<Item>();

                // Remove positions of all loot
                foreach (var grid in container.Grids)
                {
                    gridManager.SetOldPositions(grid, grid.ItemCollection.ToListOfLocations());
                    itemsInContainer.AddRange(grid.Items);
                    grid.RemoveAll();
                    Singleton<GridCacheClass>.Instance.Set(
                        container.Owner.ID,
                        grid as GridClassEx,
                        new string[] { }
                    );
                    controller.RaiseEvent(new GEventArgs23(grid));
                }

                // Sort items in container from largest to smallest
                itemsInContainer.Sort(
                    (item1, item2) => item2.GetItemSize().CompareTo(item1.GetItemSize())
                );

                // Sort grids in the container from smallest to largest
                var sortedGrids = SortGrids(container.Grids);

                // Go through each item and try to find a spot in the container for it. Since items are sorted largest to smallest and grids sorted from smallest to largest,
                // this should ensure that items prefer to be in slots that match their size, instead of being placed in a larger grid spots
                foreach (Item item in itemsInContainer)
                {
                    bool foundPlace = false;

                    // Go through each grid slot and try to add the item
                    foreach (var grid in sortedGrids)
                    {
                        if (!grid.Add(item).Failed)
                        {
                            foundPlace = true;
                            gridManager.AddItemToGrid(
                                grid,
                                new GridItemClass(
                                    item,
                                    ((ItemAddressExClass)item.CurrentAddress).LocationInGrid
                                )
                            );
                            Singleton<GridCacheClass>.Instance.Add(
                                container.Owner.ID,
                                grid as GridClassEx,
                                item
                            );
                            break;
                        }
                    }

                    if (!foundPlace)
                    {
                        // Sorting has failed! Rollback state of rig
                        gridManager.RollBack();
                        LootingBots.LootLog.LogError("Sort Failed");
                        return new SortErrorClass(item);
                    }
                }
                return gridManager;
            }

            LootingBots.LootLog.LogError("No container!");
            return new SortErrorClass(null);
        }

        // Sort grids in the container from smallest to largest
        public static StashGridClass[] SortGrids(StashGridClass[] grids)
        {
            // Sort grids in the container from smallest to largest
            var containerGrids = grids.ToList();
            containerGrids.Sort(
                (grid1, grid2) =>
                {
                    var grid1Size = grid1.GridHeight.Value * grid1.GridWidth.Value;
                    var grid2Size = grid2.GridHeight.Value * grid2.GridWidth.Value;
                    return grid1Size.CompareTo(grid2Size);
                }
            );
            return containerGrids.ToArray();
        }

        /**
        * Calculates the amount of empty grid slots in the container
        */
        public static int GetAvailableGridSlots(StashGridClass[] grids)
        {
            if (grids == null)
            {
                grids = new StashGridClass[] { };
            }

            List<StashGridClass> gridList = grids.ToList();
            return gridList.Aggregate(
                0,
                (freeSpaces, grid) =>
                {
                    int gridSize = grid.GridHeight.Value * grid.GridWidth.Value;
                    int containedItemSize = grid.GetSizeOfContainedItems();
                    freeSpaces += gridSize - containedItemSize;
                    return freeSpaces;
                }
            );
        }

        /**
        * Returns an array of available grid slots, omitting 1 free 1x2 slot. This is to ensure no loot is placed in this slot and the grid space is only used for reloaded mags
        */
        public static StashGridClass[] Reserve2x1Slot(StashGridClass[] grids)
        {
            List<StashGridClass> gridList = grids.ToList();
            foreach (var grid in gridList)
            {
                int gridSize = grid.GridHeight.Value * grid.GridWidth.Value;
                bool isLargeEnough = gridSize >= RESERVED_SLOT_COUNT;

                // If the grid is larger than 2 spaces, and the amount of free space in the grid is greater or equal to 2
                // reserve the grid as a place where the bot can place reloaded mags
                if (isLargeEnough && gridSize - grid.GetSizeOfContainedItems() >= 2)
                {
                    gridList.Remove(grid);
                    return gridList.ToArray();
                }
            }

            return gridList.ToArray();
        }

        /** Return the amount of spaces taken up by all the items in a given grid slot */
        public static int GetSizeOfContainedItems(this StashGridClass grid)
        {
            return grid.Items.Aggregate(0, (sum, item2) => sum + item2.GetItemSize());
        }

        /** Gets the size of an item in a grid */
        public static int GetItemSize(this Item item)
        {
            var dimensions = item.CalculateCellSize();
            return dimensions.X * dimensions.Y;
        }

        /** Given an item that is stackable and can be merged, search through the inventory and find any matches of that item that are not in a secure container. */
        public static Item FindItemToMerge(this InventoryControllerClass controller, Item item)
        {
            // Return null if item cannot be stacked
            if (item.StackMaxSize <= 1)
            {
                return null;
            }

            // Use the item's template id to search for the same item in the inventory
            var mergeTarget = controller.Inventory
                .GetAllItemByTemplate(item.TemplateId)
                .FirstOrDefault(
                    (foundItem) =>
                    {
                        // We dont want bots to stack loot in their secure containers
                        bool isSecureContainer = foundItem
                            .GetRootItem()
                            .Parent.Container.ID.ToLower()
                            .Equals("securedcontainer");

                        // In order for an item to be considered a valid merge target, the sum of the 2 stacks being merged must not exceed the maximum stack size
                        return !isSecureContainer
                            && (
                                item.StackObjectsCount + foundItem.StackObjectsCount
                                <= foundItem.StackMaxSize
                            );
                    }
                );

            return mergeTarget;
        }

        // Custom extension for EFT InventoryControllerClass.FindGridToPickUp that uses a custom method for choosing the grid slot to place a loot item
        public static ItemAddressExClass FindGridToPickUp(
            this InventoryControllerClass controller,
            Item item,
            IEnumerable<StashGridClass> grids = null
        )
        {
            var prioritzedGrids =
                grids ?? controller.Inventory.Equipment.GetPrioritizedGridsForLoot(item);
            foreach (var grid in prioritzedGrids)
            {
                var address = grid.FindFreeSpace(item);
                if (address != null)
                {
                    return new ItemAddressExClass(grid, address);
                }
            }

            return null;
        }

        /**
       *   Returns the list of slots to loot from a corpse in priority order. When a bot already has a backpack/rig, they will attempt to loot the weapons off the bot first. Otherwise they will loot the equipement first and loot the weapons afterwards.
       */
        public static IEnumerable<Slot> GetPrioritySlots(InventoryControllerClass targetInventory)
        {
            bool hasBackpack =
                targetInventory.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem
                != null;
            bool hasTacVest =
                targetInventory.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem != null;

            IEnumerable<EquipmentSlot> prioritySlots = new EquipmentSlot[0];
            IEnumerable<EquipmentSlot> weaponSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                new EquipmentSlot[]
                {
                    EquipmentSlot.Holster,
                    EquipmentSlot.FirstPrimaryWeapon,
                    EquipmentSlot.SecondPrimaryWeapon
                }
            );
            IEnumerable<EquipmentSlot> storageSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                new EquipmentSlot[]
                {
                    EquipmentSlot.Backpack,
                    EquipmentSlot.ArmorVest,
                    EquipmentSlot.TacticalVest,
                    EquipmentSlot.Pockets
                }
            );

            IEnumerable<EquipmentSlot> otherSlots = GetUnlockedEquipmentSlots(
                targetInventory,
                new EquipmentSlot[]
                {
                    EquipmentSlot.Headwear,
                    EquipmentSlot.Earpiece,
                    EquipmentSlot.Dogtag,
                    EquipmentSlot.Scabbard,
                    EquipmentSlot.FaceCover
                }
            );

            if (hasBackpack || hasTacVest)
            {
                prioritySlots = prioritySlots.Concat(weaponSlots).Concat(storageSlots).ToArray();
            }
            else
            {
                prioritySlots = prioritySlots.Concat(storageSlots).Concat(weaponSlots).ToArray();
            }

            prioritySlots = prioritySlots.Concat(otherSlots).ToArray();

            return targetInventory.Inventory.Equipment.GetSlotsByName(prioritySlots);
        }

        /** Given a list of slots, return all slots that are not flagged as Locked */
        private static IEnumerable<EquipmentSlot> GetUnlockedEquipmentSlots(
            InventoryControllerClass targetInventory,
            EquipmentSlot[] desiredSlots
        )
        {
            return desiredSlots.Where(
                slot => targetInventory.Inventory.Equipment.GetSlot(slot).Locked == false
            );
        }

        /** Given a LootItemClass that has slots, return any items that are listed in slots flagged as "Locked" */
        public static IEnumerable<Item> GetAllLockedItems(LootItemClass itemWithSlots)
        {
            return itemWithSlots.Slots?.Where(slot => slot.Locked).SelectMany(slot => slot.Items);
        }

        // Custom extension for EFT EquipmentClass.GetPrioritizedGridsForLoot which sorts the tacVest/backpack and reserves a 1x2 grid slot in the tacvest before finding an available grid space for loot
        public static IEnumerable<StashGridClass> GetPrioritizedGridsForLoot(
            this EquipmentClass equipment,
            Item item
        )
        {
            SearchableItemClass tacVest = (SearchableItemClass)
                equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem;
            SearchableItemClass backpack = (SearchableItemClass)
                equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem;
            SearchableItemClass pockets = (SearchableItemClass)
                equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem;
            SearchableItemClass secureContainer = (SearchableItemClass)
                equipment.GetSlot(EquipmentSlot.SecuredContainer).ContainedItem;

            StashGridClass[] tacVestGrids = new StashGridClass[0];
            if (tacVest != null)
            {
                var sortedGrids = SortGrids(tacVest.Grids);
                tacVestGrids = Reserve2x1Slot(sortedGrids);
            }

            StashGridClass[] backpackGrids =
                (backpack != null) ? SortGrids(backpack.Grids) : new StashGridClass[0];
            StashGridClass[] pocketGrids =
                (pockets != null) ? pockets.Grids : new StashGridClass[0];
            StashGridClass[] secureContainerGrids =
                (secureContainer != null) ? secureContainer.Grids : new StashGridClass[0];

            if (item is BulletClass || item is MagazineClass)
            {
                return tacVestGrids
                    .Concat(pocketGrids)
                    .Concat(backpackGrids)
                    .Concat(secureContainerGrids);
            }
            else if (item is GrenadeClass)
            {
                return pocketGrids
                    .Concat(tacVestGrids)
                    .Concat(backpackGrids)
                    .Concat(secureContainerGrids);
            }
            else
            {
                return backpackGrids
                    .Concat(tacVestGrids)
                    .Concat(pocketGrids)
                    .Concat(secureContainerGrids);
            }
        }
    }
}
