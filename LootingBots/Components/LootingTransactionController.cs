using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using LootingBots.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LootingBots.Components;

public class LootingTransactionController
{
    private const float NetworkTransactionTimeout = 5f;
    private readonly TimeoutController _networkTimeout;

    private readonly InventoryController _inventoryController;
    private readonly Player _player;
    private readonly BotLog _log;

    private readonly List<Ammo> _extraAmmoScratch = [];

    private IItemOwner _rootItemOwner;

    public LootingTransactionController(BotOwner owner, InventoryController inventoryController, BotLog log)
    {
        _inventoryController = inventoryController;
        _player = owner.GetPlayer;
        _log = log;
        _networkTimeout = _player.gameObject.AddComponent<TimeoutController>();
        _player.OnIPlayerDeadOrUnspawn += DestroyNetworkTimeoutController;
    }

    /// <summary>
    /// Tries to add extra spare ammo for the weapon being looted into the bot's secure container,
    /// so that the bots are able to refill their mags properly in their reload logic.
    /// </summary>
    public void AddExtraAmmo(Weapon weapon)
    {
        var securedContainer = (SearchableItem)
            _inventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.SecuredContainer).ContainedItem;
        if (securedContainer is null)
        {
            if (_log.WarningEnabled)
            {
                _log.LogWarning($"Could not find secured container to check extra ammo for {weapon.Name.Localized()}");
            }
            return;
        }

        // Get the weapons chamber to check
        var weaponChamber = weapon.HasChambers ? weapon.Chambers[0] : null;
        if (weaponChamber is null)
        {
            return;
        }

        // Get all ammo items in the secured container
        // then check to see if there already is ammo that meets the weapon's caliber in the secure container
        _extraAmmoScratch.Clear();
        securedContainer.GetAllItemsNonAlloc(_extraAmmoScratch);
        foreach (var bullet in _extraAmmoScratch)
        {
            if (weaponChamber.CanAccept(bullet))
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Already has ammo for {weapon.Name.Localized()}");
                }
                return; // Early exit as soon as a match is found
            }
        }

        // If we don't have any ammo,
        // attempt to add 10 max ammo stacks into the bot's secure container for use in the bot's internal reloading code
        if (_log.DebugEnabled)
        {
            _log.LogDebug($"Trying to add extra ammo for new weapon {weapon.Name.Localized()}");
        }

        // Try to get the current ammo used by the weapon by checking the weapon's chamber.
        // If it's empty, check the contents of the magazine.
        // If it's still empty, try to create an instance of the ammo using the Weapon's CurrentAmmoTemplate.
        var ammoToAdd =
            weaponChamber.ContainedItem
            ?? weapon.GetCurrentMagazine()?.FirstRealAmmo()
            ?? Singleton<ItemFactory>.Instance.CreateItem(MongoID.Generate(), weapon.CurrentAmmoTemplate._id, null);

        var ammoAdded = 0;
        var container = securedContainer.Grids[0];

        for (var i = 0; i < 10; i++)
        {
            var ammo = ammoToAdd.CloneItem();

            // Limit the stack to 60, some mods modify the max stack size to huge amounts
            ammo.StackObjectsCount = Mathf.Min(60, ammo.StackMaxSize);

            var location = container.FindFreeSpace(ammo);
            if (location != null)
            {
                var result = container.AddItemWithoutRestrictions(ammo, location);
                if (result.Succeeded)
                {
                    ammoAdded += ammo.StackObjectsCount;
                    FikaHandler.TrySendAmmoAddedPacket(_player, ammo);
                }
                else if (_log.ErrorEnabled)
                {
                    _log.LogError($"Failed to add {ammo.Name.Localized()} to secure container: {result.Error}");
                }
            }
            else if (_log.DebugEnabled)
            {
                _log.LogDebug($"Cannot find location in secure container for {ammo.Name.Localized()}");
            }
        }

        if (ammoAdded > 0 && _log.DebugEnabled)
        {
            _log.LogDebug(
                $"Successfully added {ammoAdded} rounds of {ammoToAdd.Name.Localized()} for new weapon {weapon.Name.Localized()}"
            );
        }
    }

    /// <summary>
    /// Tries to find an open Slot to equip the current item to. If a slot is found, issue a move action to equip the item.
    /// </summary>
    public Task<bool> TryEquipItemAsync(Item item, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        // Check to see if we can equip the item
        var ableToEquip = _inventoryController.FindSlotToPickUp(item);
        if (ableToEquip is null)
        {
            if (_log.DebugEnabled)
            {
                _log.LogDebug($"Could not find a place to equip: {item.Name.Localized()}");
            }
            return Task.FromResult(false);
        }

        if (_log.InfoEnabled)
        {
            _log.LogInfo($"Equipping: {item.Name.Localized()} [place: {ableToEquip.Container.ID.Localized()}]");
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
        var mergeableItem = _inventoryController.FindItemToMerge(item);
        if (mergeableItem != null)
        {
            return MergeItemAsync(item, mergeableItem, token);
        }

        // Otherwise, find an empty grid slot to put the item in
        var gridAddress = _inventoryController.FindGridToPickUp(item);
        if (
            gridAddress != null
            && !string.Equals(gridAddress.GetRootItem()?.Parent?.Container?.ID, "securedcontainer", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (_log.InfoEnabled)
            {
                _log.LogInfo($"Picking up: {item.Name.Localized()} [place: {gridAddress.GetRootItem()?.Name.Localized()}]");
            }
            return MoveItemAsync(item, gridAddress, token);
        }

        if (_log.DebugEnabled)
        {
            _log.LogDebug($"Could not find a place to pickup: {item.Name.Localized()}");
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
        if (location is null)
        {
            return await TryEquipItemAsync(item, token) || await TryPickupItemAsync(item, token);
        }

        if (_log.DebugEnabled)
        {
            _log.LogDebug(
                $"Moving {item.Name.Localized()} to: {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]..."
            );
        }

        await SimulatePlayerDelayAsync(token: token);

        if (!IsItemReachable(item))
        {
            return false;
        }

        var moveResult = ItemManipulator.Move(item, location, _inventoryController, true);
        if (moveResult.Failed)
        {
            if (_log.WarningEnabled)
            {
                _log.LogWarning(
                    $"Failed to move {item.Name.Localized()} to {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]. Error: {moveResult.Error}"
                );
            }
            return false;
        }

        var moveNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(moveResult);
        if (moveNetworkResult.Failed)
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError(
                    $"Failed to move {item.Name.Localized()} to {location.Container.ID.Localized()} [{location.GetRootItem()?.Name.Localized()}]. Network Error: {moveNetworkResult.Error}"
                );
            }
            return false;
        }

        if (_log.InfoEnabled)
        {
            _log.LogInfo(
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

        if (_log.DebugEnabled)
        {
            _log.LogDebug($"Swapping {item.Name.Localized()} with {toSwap.Name.Localized()}...");
        }

        await SimulatePlayerDelayAsync(token: token);

        if (!IsItemReachable(item))
        {
            return false;
        }

        var swapResult = ItemManipulator.Swap(item, toSwap.CurrentAddress, toSwap, item.CurrentAddress, _inventoryController, true);
        if (swapResult.Failed)
        {
            if (_log.WarningEnabled && swapResult.Error is not Slot.ConflictingItemError)
            {
                _log.LogWarning($"Failed to swap {item.Name.Localized()} with {toSwap.Name.Localized()}. Error: {swapResult.Error}");
            }
            return false;
        }

        var swapNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(swapResult);
        if (swapNetworkResult.Failed)
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError(
                    $"Failed to swap {item.Name.Localized()} with {toSwap.Name.Localized()}. Network Error: {swapNetworkResult.Error}"
                );
            }
            return false;
        }

        if (_log.InfoEnabled)
        {
            _log.LogInfo($"Swapping {item.Name.Localized()} with {toSwap.Name.Localized()}...done");
        }
        return true;
    }

    /// <summary>
    /// Attempts to merge an item stack with another specified item stack.
    /// </summary>
    public async Task<bool> MergeItemAsync(Item toMove, Item toItem, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (toItem is null)
        {
            if (_log.WarningEnabled)
            {
                _log.LogWarning($"Cannot merge item {toMove} to NULL target item!");
            }
            return false;
        }

        if (_log.DebugEnabled)
        {
            _log.LogDebug(
                $"Merging {toMove.Name.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount})..."
            );
        }

        if (!IsItemReachable(toMove))
        {
            return false;
        }

        var mergeResult = ItemManipulator.Merge(toMove, toItem, _inventoryController, true);
        if (mergeResult.Failed)
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError(
                    $"Failed to merge {toMove.Name.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount}). Error: {mergeResult.Error}"
                );
            }
            return false;
        }

        await SimulatePlayerDelayAsync(token: token);
        var mergeNetworkResult = await TryRunNetworkTransactionWithTimeoutAsync(mergeResult);
        if (mergeNetworkResult.Failed)
        {
            if (_log.ErrorEnabled)
            {
                _log.LogError(
                    $"Failed to merge {toMove.Name.Localized()} (Stack Size: {toMove.StackObjectsCount}) with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount}). Network Error: {mergeNetworkResult.Error}"
                );
            }
            return false;
        }

        if (_log.InfoEnabled)
        {
            _log.LogInfo($"Merged with: {toItem.Name.Localized()} (Stack Size: {toItem.StackObjectsCount})...done");
        }
        return true;
    }

    /// <summary>
    /// Throw an item.
    /// </summary>
    public async Task<bool> ThrowItemAsync(Item toThrow, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (_log.DebugEnabled)
        {
            _log.LogDebug($"Throwing item: {toThrow.Name.Localized()}...");
        }

        await SimulatePlayerDelayAsync(token: token);

        var promise = new TaskCompletionSource<IResult>();
        _inventoryController.ThrowItem(toThrow, false, promise.SetResult);

        var throwResult = await promise.Task;
        if (throwResult.Failed)
        {
            if (_log.WarningEnabled)
            {
                _log.LogWarning($"Failed to throw item: {toThrow.Name.Localized()}. Error: {throwResult.Error}");
            }
            return false;
        }

        if (_log.InfoEnabled)
        {
            _log.LogInfo($"Throwing item: {toThrow.Name.Localized()}...done");
        }
        return true;
    }

    /// <summary>
    /// Try to run network transaction with timeout.
    ///
    /// For some reason <see cref="InventoryController.TryRunNetworkTransaction"/>
    /// runs indefinitely when moving the bot's active weapon around.
    /// Circumvent it by checking if the operation was successful after a timeout.
    /// </summary>
    public Task<IResult> TryRunNetworkTransactionWithTimeoutAsync(OperationResult operationResult)
    {
        if (operationResult.Failed)
        {
            return Task.FromResult<IResult>(new FailedResult(operationResult.Error!.ToString()));
        }
        if (operationResult.Value.CanExecute(_inventoryController))
        {
            return RunNetworkTransactionWithTimeoutAsync(operationResult);
        }
        return Task.FromResult<IResult>(new FailedResult("InventoryController cannot execute this operation"));
    }

    /// <summary>
    /// A modified <see cref="InventoryController.RunNetworkTransaction"/> that includes a timeout
    /// </summary>
    private async Task<IResult> RunNetworkTransactionWithTimeoutAsync(OperationResult operationResult)
    {
        var timeoutToken = _networkTimeout.Timeout(NetworkTransactionTimeout);
        using var callbackSource = new CallbackTaskCompletionSource<IResult>(timeoutToken);

        var operation = _inventoryController.ConvertOperationResultToOperation(operationResult.Value);
        _inventoryController.Execute(operation, callbackSource.TrySetResult);

        try
        {
            var result = await callbackSource.Task;
            _networkTimeout.ResetTimer();
            return result;
        }
        catch (OperationCanceledException) when (_networkTimeout.IsTimeout)
        {
            if (operation.Status is EOperationStatus.Succeeded)
            {
                return new SuccessfulResult();
            }
            operation.Dispose();
            return new FailedResult($"Timed out on network transaction, operation status: {operation.Status.ToString()}");
        }
        catch (Exception)
        {
            _networkTimeout.ResetTimer();
            throw;
        }
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

        return Task.Delay(TimeSpan.FromMilliseconds(delay), token);
    }

    /// <summary>
    /// Sets owner which <see cref="IsItemReachable"/> checks for.
    /// </summary>
    public void SetRootItemOwner(IItemOwner rootItemOwner)
    {
        _rootItemOwner = rootItemOwner;
    }

    /// <summary>
    /// Check if a bot can reach this item:
    ///   1. The item is owned by the owner of the root item the bot is looting
    ///   2. The bot owns the item (e.g. for secondary to main weapon swaps)
    ///   3. The item is a <see cref="EFT.Interactive.LootItem"/> in the world, which everyone can access
    /// </summary>
    private bool IsItemReachable(Item item)
    {
        if (item.Owner == _rootItemOwner)
        {
            return true;
        }
        if (item.Owner == _inventoryController)
        {
            return true;
        }
        if (Singleton<GameWorld>.Instance.LootItems.ContainsKey(item.Id.GetHashCode())) // Key is LootItem.GetNetId()
        {
            return true;
        }

        if (_log.DebugEnabled)
        {
            _log.LogDebug($"Cannot reach {item.Name.Localized()} [with owner: {item.Owner}, location: {item.Parent}]");
        }
        return false;
    }

    private void DestroyNetworkTimeoutController(IPlayer player)
    {
        player.OnIPlayerDeadOrUnspawn -= DestroyNetworkTimeoutController;
        Object.Destroy(_networkTimeout);
    }
}
