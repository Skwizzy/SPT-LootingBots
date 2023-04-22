using System;
using System.Threading.Tasks;

using Comfort.Common;

using EFT;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class TransactionController
    {
        readonly Log _log;
        readonly InventoryControllerClass _inventoryController;
        readonly BotOwner _botOwner;

        public TransactionController(
            BotOwner botOwner,
            InventoryControllerClass inventoryController,
            Log log
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
            public ActionCallback Callback;
            public ActionCallback OnComplete;

            public MoveAction(
                Item toMove = null,
                ItemAddress place = null,
                ActionCallback callback = null,
                ActionCallback onComplete = null
            )
            {
                ToMove = toMove;
                Place = place;
                Callback = callback;
                OnComplete = onComplete;
            }
        }

        public delegate Task ActionCallback();

        /** Tries to find an open Slot to equip the current item to. If a slot is found, issue a move action to equip the item */
        public async Task<bool> TryEquipItem(Item item)
        {
            string botName =
                $"({_botOwner.Profile.Info.Settings.Role}) {_botOwner.Profile?.Info.Nickname.TrimEnd()}";

            // Check to see if we can equip the item
            var ableToEquip = _inventoryController.FindSlotToPickUp(item);
            if (ableToEquip != null)
            {
                _log.LogWarning(
                    $"{botName} is equipping: {item.Name.Localized()} [place: {ableToEquip.Container.ID.Localized()}]"
                );
                await MoveItem(new MoveAction(item, ableToEquip));

                return true;
            }

            _log.LogDebug($"Cannot equip: {item.Name.Localized()}");

            return false;
        }

        /** Tries to find a valid grid for the item being looted. Checks all containers currently equipped to the bot. If there is a valid grid to place the item inside of, issue a move action to pick up the item */
        public async Task<bool> TryPickupItem(Item item)
        {
            string botName =
                $"({_botOwner.Profile.Info.Settings.Role}) {_botOwner.Profile?.Info.Nickname.TrimEnd()}";
            var ableToPickUp = _inventoryController.FindGridToPickUp(item, _inventoryController);

            if (
                ableToPickUp != null
                && !ableToPickUp
                    .GetRootItem()
                    .Parent.Container.ID.ToLower()
                    .Equals("securedcontainer")
            )
            {
                _log.LogWarning(
                    $"{botName} is picking up: {item.Name.Localized()} [place: {ableToPickUp.GetRootItem().Name.Localized()}]"
                );
                await MoveItem(new MoveAction(item, ableToPickUp));
                return true;
            }

            _log.LogDebug($"No valid slot found for: {item.Name.Localized()}");

            return false;
        }

        /** Moves an item to a specified item address. Supports executing a callback */
        public async Task MoveItem(MoveAction moveAction)
        {
            try
            {
                _log.LogDebug($"Moving item to: {moveAction.Place.Container.ID.Localized()}");
                var value = GClass2429.Move(
                    moveAction.ToMove,
                    moveAction.Place,
                    _inventoryController,
                    true
                );

                if (moveAction.Callback == null)
                {
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
                                await moveAction.Callback();
                                promise.TrySetResult(result);
                            }
                        )
                    );

                    await promise.Task;
                }

                if (moveAction.OnComplete != null)
                {
                    await moveAction.OnComplete();
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
        }

        /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
        public async Task ThrowAndEquip(SwapAction swapAction)
        {
            try
            {
                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();
                Item toThrow = swapAction.ToThrow;

                // Potentially use GClass2426.Swap instead?

                _log.LogWarning($"Throwing item: {toThrow.Name.Localized()}");
                _inventoryController.ThrowItem(
                    toThrow,
                    null,
                    new Callback(
                        async (IResult result) =>
                        {
                            if (result.Succeed)
                            {
                                await swapAction.Callback();
                            }

                            promise.TrySetResult(result);
                        }
                    ),
                    false
                );

                await promise.Task;

                if (swapAction.OnComplete != null)
                {
                    await swapAction.OnComplete();
                }
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
        }
    }
}