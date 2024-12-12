using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;

using InventoryControllerResultStruct = GStruct445;
using GridClassEx = GClass2516;
using GridCacheClass = GClass1401;

namespace LootingBots.Patch.Components
{
    public class TransactionController
    {
        readonly BotLog _log;
        readonly EFT.InventoryLogic.InventoryController _inventoryController;
        readonly BotOwner _botOwner;
        public bool Enabled;

        public TransactionController(
            BotOwner botOwner,
            EFT.InventoryLogic.InventoryController inventoryController,
            BotLog log
        )
        {
            _botOwner = botOwner;
            _inventoryController = inventoryController;
            _log = log;
        }

        public class EquipAction
        {
            public SwapAction Swap;
            public MoveAction Move;
        }

        public class SwapAction
        {
            public Item ToThrow;
            public Item ToEquip;
            public ActionCallback Callback;
            public ActionCallback OnComplete;

            public SwapAction(
                Item toThrow = null,
                Item toEquip = null,
                ActionCallback callback = null,
                ActionCallback onComplete = null
            )
            {
                ToThrow = toThrow;
                ToEquip = toEquip;
                Callback = callback;
                OnComplete = onComplete;
            }
        }

        public class MoveAction
        {
            public Item ToMove;
            public ItemAddress Place;
            public Item ToItem;
            public ActionCallback Callback;
            public ActionCallback OnComplete;

            public MoveAction(
                Item toMove = null,
                ItemAddress place = null,
                Item toItem = null,
                ActionCallback callback = null,
                ActionCallback onComplete = null
            )
            {
                ToMove = toMove;
                Place = place;
                ToItem = toItem;
                Callback = callback;
                OnComplete = onComplete;
            }
        }

        public delegate Task ActionCallback();

        /** Tries to add extra spare ammo for the weapon being looted into the bot's secure container so that the bots are able to refill their mags properly in their reload logic */
        public bool AddExtraAmmo(Weapon weapon)
        {
            try
            {
                SearchableItemItemClass secureContainer = (SearchableItemItemClass)
                    _inventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.SecuredContainer)
                        .ContainedItem;

                // Try to get the current ammo used by the weapon by checking the contents of the magazine. If its empty, try to create an instance of the ammo using the Weapon's CurrentAmmoTemplate
                Item ammoToAdd =
                    weapon.GetCurrentMagazine()?.FirstRealAmmo()
                    ?? Singleton<ItemFactoryClass>.Instance.CreateItem(
                        MongoID.Generate(),
                        weapon.CurrentAmmoTemplate._id,
                        null
                    );

                // Check to see if there already is ammo that meets the weapon's caliber in the secure container
                bool alreadyHasAmmo =
                    secureContainer
                        .GetAllItems()
                        .Where(
                            item =>
                                item is AmmoItemClass bullet
                                && bullet.Caliber.Equals(((AmmoItemClass)ammoToAdd).Caliber)
                        )
                        .ToArray()
                        .Length > 0;

                // If we dont have any ammo, attempt to add 10 max ammo stacks into the bot's secure container for use in the bot's internal reloading code
                if (!alreadyHasAmmo)
                {
                    if (_log.DebugEnabled)
                        _log.LogDebug($"Trying to add ammo");

                    int ammoAdded = 0;

                    for (int i = 0; i < 10; i++)
                    {
                        Item ammo = ammoToAdd.CloneItem();
                        ammo.StackObjectsCount = ammo.StackMaxSize;

                        GClass3115 location = _inventoryController.FindGridToPickUp(ammoToAdd);

                        if (location != null)
                        {
                            var result = location.AddWithoutRestrictions(ammo);
                            if (result.Succeeded)
                            {
                                ammoAdded += ammo.StackObjectsCount;

                                /*
                                Singleton<GridCacheClass>.Instance.Add(
                                    location.GetOwner().ID,
                                    location.Grid as GridClassEx,
                                    ammo
                                );

                                */
                            }
                            else if (_log.ErrorEnabled)
                            {
                                _log.LogError(
                                    $"Failed to add {ammo.Name.Localized()} to secure container"
                                );
                            }
                        }
                        else if (_log.ErrorEnabled)
                        {
                            _log.LogError(
                                $"Cannot find location in secure container for {ammo.Name.Localized()}"
                            );
                        }
                    }

                    if (ammoAdded > 0 && _log.DebugEnabled)
                    {
                        _log.LogDebug(
                            $"Successfully added {ammoAdded} round of {ammoToAdd.Name.Localized()}"
                        );
                    }
                }
                else if (_log.DebugEnabled)
                {
                    _log.LogDebug($"Already has ammo for {weapon.Name.Localized()}");
                }

                return true;
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }

            return false;
        }

        /** Tries to find an open Slot to equip the current item to. If a slot is found, issue a move action to equip the item */
        public async Task<bool> TryEquipItem(Item item)
        {
            try
            {
                // Check to see if we can equip the item
                var ableToEquip = _inventoryController.FindSlotToPickUp(item);
                if (ableToEquip != null)
                {
                    if (_log.WarningEnabled)
                        _log.LogWarning(
                            $"Equipping: {item.Name.Localized()} [place: {ableToEquip.Container.ID.Localized()}]"
                        );
                    bool success = await MoveItem(new MoveAction(item, ableToEquip));
                    return success;
                }

                if (_log.DebugEnabled)
                    _log.LogDebug($"Cannot equip: {item.Name.Localized()}");
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }

            return false;
        }

        /** Tries to find a valid grid for the item being looted. Checks all containers currently equipped to the bot. If there is a valid grid to place the item inside of, issue a move action to pick up the item */
        public async Task<bool> TryPickupItem(Item item)
        {
            try
            {
                // Check to see if this is an item that we can merge with another item in the inventory
                var mergeableItem = _inventoryController.FindItemToMerge(item);

                if (mergeableItem != null)
                {
                    if (_log.WarningEnabled)
                        _log.LogWarning(
                            $"Merging: {item.Name.Localized()} [with: {mergeableItem.Name.Localized()}]"
                        );

                    return await MergeItem(new MoveAction(item, null, mergeableItem));
                }

                // Otherwise, find an empty grid slot to put the item in
                var gridAddress = _inventoryController.FindGridToPickUp(item);

                if (
                    gridAddress != null
                    && !gridAddress
                        .GetRootItem()
                        .Parent.Container.ID.ToLower()
                        .Equals("securedcontainer")
                )
                {
                    if (_log.WarningEnabled)
                        _log.LogWarning(
                            $"Picking up: {item.Name.Localized()} [place: {gridAddress.GetRootItem().Name.Localized()}]"
                        );

                    return await MoveItem(new MoveAction(item, gridAddress));
                }

                if (_log.DebugEnabled)
                    _log.LogDebug($"No valid slot found for: {item.Name.Localized()}");
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }
            return false;
        }

        /** Moves an item to a specified item address. Supports executing a callback */
        public async Task<bool> MoveItem(MoveAction moveAction)
        {
            try
            {
                if (IsLootingInterrupted())
                {
                    return false;
                }

                if (moveAction.ToMove is Weapon weapon && !(moveAction.ToMove is AmmoItemClass))
                {
                    //Archangel: todo: This is broke at the moment due to methods of 3.9 no longer existing here, would need a re-do
                    //AddExtraAmmo(weapon);
                }

                if (_log.DebugEnabled)
                {
                    _log.LogDebug(
                        $"Moving item to: {moveAction?.Place?.Container?.ID?.Localized()}"
                    );
                }

                var value = InteractionsHandlerClass.Move(
                    moveAction.ToMove,
                    moveAction.Place,
                    _inventoryController,
                    true
                );

                if (value.Failed)
                {
                    if (_log.ErrorEnabled)
                    {
                        _log.LogError(
                            $"Failed to move {moveAction.ToMove.Name.Localized()} to {moveAction.Place.Container.ID.Localized()}"
                        );
                    }
                    return false;
                }

                if (moveAction.Callback == null)
                {
                    await SimulatePlayerDelay();
                    await _inventoryController.TryRunNetworkTransaction(value, null);
                }
                else
                {
                    TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

                    await _inventoryController.TryRunNetworkTransaction(
                        value,
                        new Callback(
                            async (IResult result) =>
                            {
                                if (result.Succeed)
                                {
                                    await SimulatePlayerDelay();
                                    await moveAction.Callback();
                                }
                                promise.TrySetResult(result);
                            }
                        )
                    );

                    await promise.Task;
                }
                if (moveAction.OnComplete != null)
                {
                    await SimulatePlayerDelay();
                    await moveAction.OnComplete();
                }
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }

            return true;
        }

        /** Attempts to merge an item stack with another specified item stack. Supports executing a callback */
        public async Task<bool> MergeItem(MoveAction moveAction)
        {
            try
            {
                if (IsLootingInterrupted())
                {
                    return false;
                }

                if (_log.DebugEnabled)
                {
                    _log.LogDebug(
                        $"Merging {moveAction?.ToMove?.Name?.Localized()} (Stack Size: {moveAction?.ToMove?.StackObjectsCount}) with: {moveAction?.ToItem?.Name?.Localized()} (Stack Size: {moveAction?.ToItem?.StackObjectsCount})"
                    );
                }

                var value = InteractionsHandlerClass.Merge(
                    moveAction.ToMove,
                    moveAction.ToItem,
                    _inventoryController,
                    true
                );

                if (value.Failed)
                {
                    if (_log.ErrorEnabled)
                    {
                        _log.LogError(
                            $"Failed to merge {moveAction?.ToMove?.Name?.Localized()} (Stack Size: {moveAction?.ToMove?.StackObjectsCount}) with: {moveAction?.ToItem?.Name?.Localized()} (Stack Size: {moveAction?.ToItem?.StackObjectsCount})"
                        );
                    }
                    return false;
                }

                if (moveAction.Callback == null)
                {
                    await SimulatePlayerDelay();
                    await _inventoryController.TryRunNetworkTransaction(value, null);
                }
                else
                {
                    TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

                    await _inventoryController.TryRunNetworkTransaction(
                        value,
                        new Callback(
                            async (IResult result) =>
                            {
                                if (result.Succeed)
                                {
                                    await SimulatePlayerDelay();
                                    await moveAction.Callback();
                                }
                                promise.TrySetResult(result);
                            }
                        )
                    );

                    await promise.Task;
                }
                if (moveAction.OnComplete != null)
                {
                    await SimulatePlayerDelay();
                    await moveAction.OnComplete();
                }
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }

            return true;
        }

        /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
        public async Task<bool> ThrowAndEquip(SwapAction swapAction)
        {
            if (IsLootingInterrupted())
            {
                return false;
            }

            try
            {
                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();
                Item toThrow = swapAction.ToThrow;

                if (_log.WarningEnabled)
                    _log.LogWarning($"Throwing item: {toThrow.Name.Localized()}");

                _inventoryController.ThrowItem(toThrow, false,
                    new Callback(
                        async (IResult result) =>
                        {
                            if (result.Succeed && swapAction.Callback != null)
                            {
                                await SimulatePlayerDelay();
                                await swapAction.Callback();
                            }

                            promise.TrySetResult(result);
                        }
                    )
                );

                await SimulatePlayerDelay();
                IResult taskResult = await promise.Task;
                if (taskResult.Failed)
                {
                    return false;
                }

                if (swapAction.OnComplete != null)
                {
                    await swapAction.OnComplete();
                }

                return true;
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }

            return false;
        }

        public Task<IResult> TryRunNetworkTransaction(
            InventoryControllerResultStruct operationResult,
            Callback callback = null
        )
        {
            return _inventoryController.TryRunNetworkTransaction(operationResult, callback);
        }

        public bool IsLootingInterrupted()
        {
            return !Enabled;
        }

        public static Task SimulatePlayerDelay(float delay = -1f)
        {
            if (delay == -1)
            {
                delay = LootingBots.TransactionDelay.Value;
            }

            return Task.Delay(TimeSpan.FromMilliseconds(delay));
        }
    }
}
