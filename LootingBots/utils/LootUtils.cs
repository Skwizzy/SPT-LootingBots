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
                foreach (GClass2166 grid in container.Grids)
                {
                    gridManager.SetOldPositions(grid, grid.ItemCollection.ToListOfLocations());
                    itemsInContainer.AddRange(grid.Items);
                    grid.RemoveAll();
                    controller.RaiseEvent(new GEventArgs23(grid));
                }

                // Sort items in container from largest to smallest
                itemsInContainer.Sort(
                    (item1, item2) =>
                    {
                        var item1Dimensions = item1.CalculateCellSize();
                        var item2Dimensions = item2.CalculateCellSize();
                        int item1Size = item1Dimensions.X * item1Dimensions.Y;
                        int item2Size = item2Dimensions.X * item2Dimensions.Y;
                        return item2Size.CompareTo(item1Size);
                    }
                );

                // Sort grids in the container from smallest to largest
                var containerGrids = container.Grids.ToList();
                containerGrids.Sort(
                    (grid1, grid2) =>
                    {
                        var grid1Size = grid1.GridHeight.Value * grid1.GridWidth.Value;
                        var grid2Size = grid2.GridHeight.Value * grid2.GridWidth.Value;
                        return grid1Size.CompareTo(grid2Size);
                    }
                );

                // Go through each item and try to find a spot in the container for it. Since items are sorted largest to smallest and grids sorted from smallest to largest,
                // this should ensure that items prefer to be in slots that match their size, instead of being placed in a larger grid spots
                foreach (Item item in itemsInContainer)
                {
                    bool foundPlace = false;

                    // Go through each grid slot and try to add the item
                    foreach (GClass2166 grid in containerGrids)
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
    }
}
