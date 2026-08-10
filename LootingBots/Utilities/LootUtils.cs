using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LootingBots.Utilities;

public static class LootUtils
{
    public const int RESERVED_SLOT_COUNT = 2;
    public static readonly int LowPolyMask = LayerMask.GetMask("LowPolyCollider");
    public static readonly int LootMask = LayerMask.GetMask("Interactive", "Loot", "Deadbody");
    public static readonly AccessTools.FieldRef<Player, Corpse> _playerCorpseField = AccessTools.FieldRefAccess<Player, Corpse>("Corpse");

    private static readonly EquipmentSlot[] WeaponSlots =
    [
        EquipmentSlot.Holster,
        EquipmentSlot.FirstPrimaryWeapon,
        EquipmentSlot.SecondPrimaryWeapon,
    ];

    private static readonly EquipmentSlot[] StorageSlots =
    [
        EquipmentSlot.Backpack,
        EquipmentSlot.ArmorVest,
        EquipmentSlot.TacticalVest,
        EquipmentSlot.Pockets,
    ];

    private static readonly EquipmentSlot[] OtherSlots =
    [
        EquipmentSlot.ArmBand,
        EquipmentSlot.Headwear,
        EquipmentSlot.Earpiece,
        EquipmentSlot.Dogtag,
        EquipmentSlot.Scabbard,
        EquipmentSlot.FaceCover,
        EquipmentSlot.Eyewear,
    ];

    /// <summary>
    /// Calculate the size of a container
    /// </summary>
    public static int GetContainerSize(this EFT.InventoryLogic.SearchableItem container)
    {
        var grids = container.Grids;
        var gridSize = 0;

        foreach (var grid in grids)
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
        var key = item.GetItemComponent<KeyComponent>();
        return key != null && key.Template.MaximumNumberOfUsage == 1;
    }

    /// <summary>
    /// Triggers a container to open/close. Borrowed from Questing Bots, needed for Fika
    /// </summary>
    /// <seealso href="https://github.com/dwesterwick/SPTQuestingBots/blob/0.10.3/bepinex_dev/SPTQuestingBots/Helpers/InteractiveObjectHelpers.cs#L111"/>
    public static void InteractContainer(
        WorldInteractiveObject worldInteractiveObject,
        BotOwner botOwner,
        EInteractionType action,
        BotLog log
    )
    {
        // TODO: Null check is probably no longer needed!
        if (worldInteractiveObject == null)
        {
            if (log.DebugEnabled)
            {
                log.LogWarning($"Interacting [{action.ToString()}] with WorldInteractiveObject but is NULL");
            }
            return;
        }

        var interactionResult = new InteractionResult(action);
        if (worldInteractiveObject is Door)
        {
            // NOTE: This method MUST be used for Fika compatibility
            botOwner.GetPlayer.StartInteraction(worldInteractiveObject, interactionResult, null);
        }

        // NOTE: This method MUST be used for Fika compatibility
        botOwner.GetPlayer.ExecuteInteraction(worldInteractiveObject, interactionResult);
    }

    /// <summary>
    /// Calculates the amount of empty grid slots in the container
    /// </summary>
    public static int GetAvailableGridSlots(EFT.InventoryLogic.Grid[] grids)
    {
        if (grids is null)
        {
            return 0;
        }

        // Initialize freeSpaces to 0
        var freeSpaces = 0;

        // Loop through each grid and calculate the free spaces
        foreach (var grid in grids)
        {
            var gridSize = grid.GridHeight * grid.GridWidth;
            var containedItemSize = grid.GetSizeOfContainedItems();
            freeSpaces += gridSize - containedItemSize;
        }

        return freeSpaces;
    }

    /// <summary>
    /// returns the amount of space taken up by all the items in a given grid slot
    /// </summary>
    /// <param name="grid">The grid to calculate the amount of space taken up for</param>
    /// <returns>Returns the item size as an integer</returns>
    public static int GetSizeOfContainedItems(this EFT.InventoryLogic.Grid grid)
    {
        var containedItemSize = 0;

        // Loop through each item in grid.Items and accumulate the item size
        foreach (var item in grid.Items)
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

    /// <summary>
    /// Given an item that is stackable and can be merged,
    /// search through the inventory and find any matches of that item that are not in a secure container.
    /// </summary>
    public static Item FindItemToMerge(this InventoryController controller, Item item)
    {
        // Return null if item cannot be stacked
        if (item.StackMaxSize <= 1)
        {
            return null;
        }

        // Use the item's template id to search for the same item in the inventory
        foreach (var foundItem in controller.Inventory.GetAllItemByTemplate(item.TemplateId))
        {
            if (foundItem == null)
            {
                continue;
            }

            var rootItem = foundItem.GetRootItem();

            // Do not try to merge with cartridges or weapon chambers
            if (foundItem.Parent.Container is StackSlot or Slot)
            {
                continue;
            }

            if (rootItem.Parent.Container.ID.Equals("securedcontainer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.StackObjectsCount + foundItem.StackObjectsCount <= foundItem.StackMaxSize)
            {
                return foundItem;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the list of slots to loot from a corpse in priority order.
    /// When a bot already has a backpack/rig, it will attempt to loot the weapons off the bot first.
    /// Otherwise, it will loot the equipment first and loot the weapons afterward.
    /// </summary>
    public static void GetPriorityItems(
        this InventoryEquipment corpseEquipment,
        InventoryEquipment botEquipment,
        List<Item> preallocatedList
    )
    {
        var hasBackpack = botEquipment.GetSlot(EquipmentSlot.Backpack).ContainedItem != null;
        var hasTacVest = botEquipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem != null;

        // Add slots in priority order
        if (hasBackpack || hasTacVest)
        {
            GetItemInSlotsNonAlloc(corpseEquipment, botEquipment, preallocatedList, WeaponSlots);
            GetItemInSlotsNonAlloc(corpseEquipment, botEquipment, preallocatedList, StorageSlots);
        }
        else
        {
            GetItemInSlotsNonAlloc(corpseEquipment, botEquipment, preallocatedList, StorageSlots);
            GetItemInSlotsNonAlloc(corpseEquipment, botEquipment, preallocatedList, WeaponSlots);
        }

        GetItemInSlotsNonAlloc(corpseEquipment, botEquipment, preallocatedList, OtherSlots);
    }

    private static void GetItemInSlotsNonAlloc(
        InventoryEquipment equipment,
        InventoryEquipment botEquipment,
        List<Item> preallocatedList,
        EquipmentSlot[] slots
    )
    {
        var equipmentOwner = equipment.Parent.GetOwner();
        var botOwner = botEquipment.Parent.GetOwner();
        foreach (var slotName in slots)
        {
            var slot = equipment.GetSlot(slotName);
            var item = slot.ContainedItem;
            if (item == null)
            {
                continue;
            }

            // Check if item is unlootable
            var unlootableComponent = item.GetItemComponent<UnlootableComponent>();
            if (
                unlootableComponent != null
                && equipmentOwner != botOwner
                && unlootableComponent.IsUnlootableFrom(item.Parent.Container)
                && item is not EFT.InventoryLogic.Pockets // Include pockets to loot list
            )
            {
                continue;
            }

            preallocatedList.Add(item);
        }
    }

    /// <summary>
    ///  Helper to get the root item of an InteractableObject
    /// </summary>
    public static Item GetRootItem(this InteractableObject interactableObject)
    {
        return interactableObject switch
        {
            LootableContainer container => container.ItemOwner?.RootItem,
            LootItem lootItem => lootItem.ItemOwner?.RootItem,
            _ => null,
        };
    }

    /// <summary>
    ///  Helper to get the root item ID of an InteractableObject
    /// </summary>
    public static string GetRootItemId(this InteractableObject interactableObject)
    {
        return interactableObject switch
        {
            LootableContainer container => container.ItemOwner?.RootItem.Id,
            LootItem lootItem => lootItem.ItemOwner?.RootItem.Id,
            _ => null,
        };
    }

    /// <summary>
    ///  Helper to get the loot name of an InteractableObject, depending on the type.
    /// </summary>
    public static string GetLootName(this InteractableObject interactableObject)
    {
        return interactableObject switch
        {
            LootableContainer container => container.ItemOwner?.RootItem.Name.Localized(),
            Corpse corpse => corpse.name,
            LootItem lootItem => lootItem.ItemOwner?.RootItem.Name.Localized(),
            _ => "-",
        };
    }

    /// <summary>
    /// Check if moving an item to a slot is blocked.
    /// Except chest/rig armor.
    /// Based on <see cref="Slot.method_3"/>
    /// </summary>
    public static bool HasBlockingItem(this Slot slot, Item incomingItem, out Item conflictingItem)
    {
        conflictingItem = null;

        var conflictingSlots = slot.ConflictingSlots;
        if (conflictingSlots is null)
        {
            return false;
        }

        if (!incomingItem.TryGetItemComponent<SlotBlockerComponent>(out var slotBlocker))
        {
            return false;
        }

        var slotNames = slotBlocker.ConflictingSlotNames;
        for (var i = 0; i < slotNames.Length; i++)
        {
            if (
                conflictingSlots.TryGetValue(slotNames[i], out var conflictingSlot)
                && conflictingSlot != slot // Exclude checking the same slot
                && conflictingSlot.ContainedItem is { } conflictItem
                && conflictItem is not EFT.InventoryLogic.Armor and not EFT.InventoryLogic.Vest // Exclude chest/rig armor
            )
            {
                conflictingItem = conflictItem;
                return true;
            }
        }

        return false;
    }
}
