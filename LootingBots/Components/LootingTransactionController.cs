using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using LootingBots.Utilities;
using InventoryControllerResultStruct = Diz.LanguageExtensions.OperationResult;

namespace LootingBots.Components;

public class LootingTransactionController(InventoryController inventoryController, BotLog log)
{
    private const int NetworkTransactionTimeout = 5000;

    /// <summary>
    /// Tries to add extra spare ammo for the weapon being looted into the bot's secure container,
    /// so that the bots are able to refill their mags properly in their reload logic.
    ///
    /// Incompatible with Fika.
    /// </summary>
    public bool AddExtraAmmo(Weapon weapon)
    {
        try
        {
            var secureContainer = (EFT.InventoryLogic.SearchableItem)
                inventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.SecuredContainer).ContainedItem;

            var container = secureContainer.Grids.FirstOrDefault();

            // Try to get the current ammo used by the weapon by checking the contents of the magazine.
            // If it's empty, try to create an instance of the ammo using the Weapon's CurrentAmmoTemplate
            var ammoToAdd =
                weapon.GetCurrentMagazine()?.FirstRealAmmo()
                ?? Singleton<EFT.ItemFactory>.Instance.CreateItem(MongoID.Generate(), weapon.CurrentAmmoTemplate._id, null);

            // Check to see if there already is ammo that meets the weapon's caliber in the secure container
            var alreadyHasAmmo = false;

            foreach (var item in secureContainer.GetAllItems())
            {
                if (item is EFT.InventoryLogic.Ammo bullet && bullet.Caliber.Equals(((EFT.InventoryLogic.Ammo)ammoToAdd).Caliber))
                {
                    alreadyHasAmmo = true;
                    break; // Early exit as soon as a match is found
                }
            }

            // If we don't have any ammo,
            // attempt to add 10 max ammo stacks into the bot's secure container for use in the bot's internal reloading code
            if (!alreadyHasAmmo)
            {
                if (log.DebugEnabled)
                {
                    log.LogDebug($"Trying to add ammo");
                }

                var ammoAdded = 0;

                for (var i = 0; i < 10; i++)
                {
                    var ammo = ammoToAdd.CloneItem();
                    ammo.StackObjectsCount = ammo.StackMaxSize;

                    var location = container.FindFreeSpace(ammo);

                    if (location != null)
                    {
                        var result = container.AddItemWithoutRestrictions(ammo, location);
                        if (result.Succeeded)
                        {
                            ammoAdded += ammo.StackObjectsCount;
                        }
                        else if (log.ErrorEnabled)
                        {
                            log.LogError($"Failed to add {ammo.Name.Localized()} to secure container");
                        }
                    }
                    else if (log.ErrorEnabled)
                    {
                        log.LogError($"Cannot find location in secure container for {ammo.Name.Localized()}");
                    }
                }

                if (ammoAdded > 0 && log.DebugEnabled)
                {
                    log.LogDebug($"Successfully added {ammoAdded} round of {ammoToAdd.Name.Localized()}");
                }
            }
            else if (log.DebugEnabled)
            {
                log.LogDebug($"Already has ammo for {weapon.Name.Localized()}");
            }
        }
        catch (Exception e)
        {
            log.LogError(e);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to find an open Slot to equip the current item to. If a slot is found, issue a move action to equip the item.
    /// </summary>
    public Task<bool> TryEquipItemAsync(Item item, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        // Check to see if we can equip the item
        var ableToEquip = inventoryController.FindSlotToPickUp(item);
        if (ableToEquip == null)
        {
            if (log.DebugEnabled)
            {
                log.LogDebug($"Could not find a place to equip: {item.Name.Localized()}");
            }
            return Task.FromResult(false);
        }

        if (log.InfoEnabled)
        {
            log.LogInfo($"Equipping: {item.Name.Localized()} [place: {ableToEquip.Container.ID.Localized()}]");
        }

        return MoveItemAsync(item, ableToEquip, token);
    }

    /// <summary>
    /// Tries to find a valid grid for the item being looted. Checks all containers currently equipped to the bot.
    /// If there is a valid grid to place the item inside, issue a merge/move action to pick up the item.
    /// </summary>
    public Task<bool> TryPickupItemAsync(Item item, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        // Check to see if this is an item that we can merge with another item in the inventory
        var mergeableItem = inventoryController.FindItemToMerge(item);

        if (mergeableItem != null)
        {
            if (log.DebugEnabled)
            {
                log.LogDebug($"Merging: {item.Name.Localized()} [with: {mergeableItem.Name.Localized()}]");
            }

            return MergeItemAsync(item, mergeableItem, token);
        }

        // Otherwise, find an empty grid slot to put the item in
        var gridAddress = inventoryController.FindGridToPickUp(item);

        if (
            gridAddress != null
            && !string.Equals(gridAddress.GetRootItem()?.Parent?.Container?.ID, "securedcontainer", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (log.InfoEnabled)
            {
                log.LogInfo($"Picking up: {item.Name.Localized()} [place: {gridAddress.GetRootItem()?.Name.Localized()}]");
            }

            return MoveItemAsync(item, gridAddress, token);
        }

        if (log.DebugEnabled)
        {
            log.LogDebug($"Could not find a place to pickup: {item.Name.Localized()}");
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Moves an item to a specified item address
    /// </summary>
    /// <param name="location">If address is null, try to equip if a slot is available, or pickup if a grid is available</param>
    public async Task<bool> MoveItemAsync(Item item, ItemAddress location, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        // No address was given, try equipping or picking up
        if (location == null)
        {
            return await TryEquipItemAsync(item, token) || await TryPickupItemAsync(item, token);
        }

        if (log.DebugEnabled)
        {
            log.LogDebug(
                $"Moving {item.Name.Localized()} to: {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]..."
            );
        }

        await SimulatePlayerDelayAsync(token: token);

        var moveResult = EFT.InventoryLogic.ItemManipulator.Move(item, location, inventoryController, true);
        if (moveResult.Failed)
        {
            if (log.ErrorEnabled)
            {
                log.LogWarning(
                    $"Failed to move {item.Name.Localized()} to {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]. Error: {moveResult.Error}"
                );
            }
            return false;
        }

        var moveNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(moveResult, null, token);
        if (moveNetworkResult.Failed)
        {
            if (log.ErrorEnabled)
            {
                log.LogError(
                    $"Failed to move {item.Name.Localized()} to {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]. Network Error: {moveNetworkResult.Error}"
                );
            }
            return false;
        }

        if (log.InfoEnabled)
        {
            log.LogInfo(
                $"Moving {item.Name.Localized()} to: {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]...done"
            );
        }

        return true;
    }

    /// <summary>
    /// Swaps an item with another item.
    /// </summary>
    /// <param name="item">Is almost always the incoming item</param>
    /// <param name="toSwap">Is almost always the swapped out/thrown out item</param>
    public async Task<bool> SwapItemsAsync(Item item, Item toSwap, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (log.DebugEnabled)
        {
            log.LogDebug($"Swapping {item.Name.Localized()} with {toSwap.Name.Localized()}...");
        }

        await SimulatePlayerDelayAsync(token: token);

        var swapResult = EFT.InventoryLogic.ItemManipulator.Swap(item, toSwap.CurrentAddress, toSwap, item.CurrentAddress, inventoryController, true);
        if (swapResult.Failed)
        {
            if (log.WarningEnabled)
            {
                log.LogWarning($"Failed to swap {item.Name.Localized()} with {toSwap.Name.Localized()}. Error: {swapResult.Error}");
            }
            return false;
        }

        var swapNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(swapResult, null, token);
        if (swapNetworkResult.Failed)
        {
            if (log.ErrorEnabled)
            {
                log.LogError(
                    $"Failed to swap {item.Name.Localized()} with {toSwap.Name.Localized()}. Network Error: {swapNetworkResult.Error}"
                );
            }
            return false;
        }

        if (log.InfoEnabled)
        {
            log.LogInfo($"Swapping {item.Name.Localized()} with {toSwap.Name.Localized()}...done");
        }

        return true;
    }

    /// <summary>
    /// Attempts to merge an item stack with another specified item stack.
    /// </summary>
    public async Task<bool> MergeItemAsync(Item toMove, Item toItem, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (toItem == null)
        {
            log.LogWarning($"Cannot merge item {toMove} to NULL target item!");
            return false;
        }

        if (log.DebugEnabled)
        {
            log.LogDebug(
                $"Merging {toMove.Name?.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount})..."
            );
        }

        var mergeResult = EFT.InventoryLogic.ItemManipulator.Merge(toMove, toItem, inventoryController, true);
        if (mergeResult.Failed)
        {
            if (log.ErrorEnabled)
            {
                log.LogError(
                    $"Failed to merge {toMove.Name.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount}). Error: {mergeResult.Error}"
                );
            }
            return false;
        }

        await SimulatePlayerDelayAsync(token: token);
        var mergeNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(mergeResult, null, token);
        if (mergeNetworkResult.Failed)
        {
            if (log.ErrorEnabled)
            {
                log.LogError(
                    $"Failed to merge {toMove.Name.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount}). Network Error: {mergeNetworkResult.Error}"
                );
            }
            return false;
        }

        if (log.InfoEnabled)
        {
            log.LogInfo(
                $"Merging {toMove.Name?.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount})...done"
            );
        }

        return true;
    }

    /// <summary>
    /// Throw an item.
    /// </summary>
    public async Task<bool> ThrowItemAsync(Item toThrow, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (log.DebugEnabled)
        {
            log.LogDebug($"Throwing item: {toThrow.Name.Localized()}...");
        }

        await SimulatePlayerDelayAsync(token: token);

        var promise = new TaskCompletionSource<IResult>();
        inventoryController.ThrowItem(toThrow, false, promise.SetResult);

        var throwResult = await promise.Task;
        if (throwResult.Failed)
        {
            if (log.WarningEnabled)
            {
                log.LogWarning($"Failed to throw item: {toThrow.Name.Localized()}. Error: {throwResult.Error}");
            }
            return false;
        }

        if (log.InfoEnabled)
        {
            log.LogInfo($"Throwing item: {toThrow.Name.Localized()}...done");
        }

        return true;
    }

    /// <summary>
    /// Try to run network transaction with timeout.
    /// For some odd reason I can't figure out, especially when moving the bot's active weapon around, the method runs indefinitely.
    /// So try to circumvent it by fast forwarding the current state.
    ///
    /// It's GClass2053 Operation (RemoveWeaponOperation) running indefinitely.
    /// </summary>
    public async Task<IResult> TryRunNetworkTransactionWithTimeoutAsync(
        InventoryControllerResultStruct operationResult,
        Callback callback = null,
        CancellationToken token = default
    )
    {
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(NetworkTransactionTimeout));

        var networkTask = inventoryController.TryRunNetworkTransaction(operationResult, callback);

        await Task.WhenAny(networkTask, Task.Delay(Timeout.Infinite, timeoutSource.Token), Task.Delay(Timeout.Infinite, token));

        if (timeoutSource.Token.IsCancellationRequested)
        {
            var playerInvCont = (Player.PlayerInventoryController)inventoryController;
            if (log.WarningEnabled)
            {
                log.LogWarning("Timed out on network transaction, trying to fast forward...");
            }
            playerInvCont.Player.FastForwardCurrentOperations();
        }
        else
        {
            token.ThrowIfCancellationRequested();
        }

        return await networkTask;
    }

    /// <summary>
    /// Simulate decisions while looting by performing a delay.
    /// </summary>
    public static Task SimulatePlayerDelayAsync(double delay = -1f, CancellationToken token = default)
    {
        if (delay == -1D)
        {
            delay = LootingBots.TransactionDelay.Value;
        }

        return Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken: token);
    }
}
