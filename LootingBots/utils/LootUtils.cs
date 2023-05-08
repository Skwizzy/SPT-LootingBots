using System.Collections.Generic;
using System.Linq;

using EFT.InventoryLogic;

namespace LootingBots.Patch.Util
{
    public static class LootUtils
    {
        public static GStruct325<GClass2463> SortContainer(
            SearchableItemClass container,
            InventoryControllerClass controller
        )
        {
            if (container != null)
            {
                List<object> newLocations = new List<object>();
                GClass2463 gridManager = new GClass2463(container, controller);
                List<Item> itemsInContainer = new List<Item>();

                // Remove positions of all loot
                foreach (var grid in container.Grids)
                {
                    gridManager.SetOldPositions(grid, grid.ItemCollection.ToListOfLocations());
                    itemsInContainer.AddRange(grid.Items);
                    grid.RemoveAll();
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
                                new GClass2174(
                                    item,
                                    ((GClass2424)item.CurrentAddress).LocationInGrid
                                )
                            );
                            break;
                        }
                    }

                    if (!foundPlace)
                    {
                        // Sorting has failed! Rollback state of rig
                        gridManager.RollBack();
                        LootingBots.LootLog.LogError("Sort Failed");
                        return new GClass2857(item);
                    }
                }
                return gridManager;
            }

            LootingBots.LootLog.LogError("No container!");
            return new GClass2857(null);
        }

        public static GClass2166[] SortGrids(GClass2166[] grids)
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

        public static GClass2166[] Reserve2x1Slot(GClass2166[] grids)
        {
            const int RESERVE_SLOT_COUNT = 2;
            List<GClass2166> gridList = grids.ToList();
            foreach (var grid in gridList)
            {
                int gridSize = grid.GridHeight.Value * grid.GridWidth.Value;
                bool isLargeEnough = gridSize >= RESERVE_SLOT_COUNT;

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

        public static int GetSizeOfContainedItems(this GClass2166 grid)
        {
            return grid.Items.Aggregate(0, (sum, item2) => sum + item2.GetItemSize());
        }

        public static int GetItemSize(this Item item)
        {
            var dimensions = item.CalculateCellSize();
            return dimensions.X * dimensions.Y;
        }

        // Custom extension for EFT InventoryControllerClass.FindGridToPickUp
        public static GClass2424 FindGridToPickUp(
            this InventoryControllerClass controller,
            Item item
        )
        {
            var prioritzedGrids = controller.Inventory.Equipment.GetPrioritizedGridsForLoot(item);

            foreach (var grid in prioritzedGrids)
            {
                var address = grid.FindFreeSpace(item);
                if (address != null)
                {
                    return new GClass2424(grid, address);
                }
            }

            return null;
        }

        // Custom extension for EFT EquipmentClass.GetPrioritizedGridsForLoot
        public static IEnumerable<GClass2166> GetPrioritizedGridsForLoot(
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

            // GClass2166 is Grid class
            GClass2166[] tacVestGrids = new GClass2166[0];
            if (tacVest != null)
            {
                var sortedGrids = SortGrids(tacVest.Grids);
                tacVestGrids = Reserve2x1Slot(sortedGrids);
            }

            GClass2166[] backpackGrids =
                (backpack != null) ? SortGrids(backpack.Grids) : new GClass2166[0];
            GClass2166[] pocketGrids = (pockets != null) ? pockets.Grids : new GClass2166[0];
            GClass2166[] secureContainerGrids =
                (secureContainer != null) ? secureContainer.Grids : new GClass2166[0];

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
