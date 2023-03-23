using EFT.InventoryLogic;
using System.Threading.Tasks;
using Comfort.Common;
using System;

namespace LootingBots.Patch.Util
{
    public class TransactionController
    {
        Log log;
        InventoryControllerClass inventoryController;

        public TransactionController(
            InventoryControllerClass inventoryController,
            BepInEx.Logging.ManualLogSource Logger
        )
        {
            this.inventoryController = inventoryController;
            this.log = new Log(Logger);
        }

        public class EquipAction
        {
            public SwapAction swap;
            public MoveAction move;
        }

        public class SwapAction
        {
            public Item toThrow;
            public Item toEquip;
            public ActionCallback callback;
            public ActionCallback onComplete;

            public SwapAction(
                Item toThrow = null,
                Item toEquip = null,
                ActionCallback callback = null,
                ActionCallback onComplete = null
            )
            {
                this.toThrow = toThrow;
                this.toEquip = toEquip;
                this.callback = callback;
                this.onComplete = onComplete;
            }
        }

        public class MoveAction
        {
            public Item toMove;
            public ItemAddress place;
            public ActionCallback callback;
            public ActionCallback onComplete;

            public MoveAction(
                Item toMove = null,
                ItemAddress place = null,
                ActionCallback callback = null,
                ActionCallback onComplete = null
            )
            {
                this.toMove = toMove;
                this.place = place;
                this.callback = callback;
                this.onComplete = onComplete;
            }
        }

        public delegate Task ActionCallback();

        /** Tries to find an open slot to equip the current item to. If a slot is found, issue a move action to equip the item */
        public async Task<bool> tryEquipItem(Item item)
        {
            // Check to see if we can equip the item
            GClass2419 ableToEquip = inventoryController.FindSlotToPickUp(item);
            if (ableToEquip != null)
            {
                log.logWarning($"Equipping to {ableToEquip.Container.ID.Localized()}: {item.Name.Localized()}");
                await moveItem(new MoveAction(item, ableToEquip));

                return true;
            }

            log.logDebug($"Cannot equip: {item.Name.Localized()}");

            return false;
        }

        /** Tries to find a valid grid for the item being looted. If there is a valid grid to place the item inside of, issue a move action to pick up the item */
        public async Task<bool> tryPickupItem(Item item)
        {
            GClass2421 ableToPickUp = inventoryController.FindGridToPickUp(
                item,
                inventoryController
            );

            if (ableToPickUp != null)
            {
                log.logWarning($"Placing item {item.Name.Localized()} in {ableToPickUp.Container.ID.Localized()}");
                await moveItem(new MoveAction(item, ableToPickUp));
                return true;
            }

            log.logDebug($"No valid slot found for: {item.Name.Localized()}");

            return false;
        }

        /** Moves an item to a specified item address. Supports executing a callback */
        public async Task moveItem(MoveAction moveAction)
        {
            try
            {
                log.logDebug(
                    $"Moving item to: {moveAction.place?.Container?.ID?.Localized()}"
                );
                GStruct321 value = GClass2426.Move(
                    moveAction.toMove,
                    moveAction.place,
                    inventoryController,
                    true
                );

                if (moveAction.callback == null)
                {
                    await inventoryController.TryRunNetworkTransaction(value, null);
                }
                else
                {
                    TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

                    await inventoryController.TryRunNetworkTransaction(
                        value,
                        new Callback(
                            async (IResult result) =>
                            {
                                await moveAction.callback();
                                promise.TrySetResult(result);
                            }
                        )
                    );

                    await promise.Task;
                }

                if (moveAction.onComplete != null)
                {
                    await moveAction.onComplete();
                }
            }
            catch (Exception e)
            {
                log.logError(e);
            }
        }

        /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
        public async Task throwAndEquip(SwapAction swapAction)
        {
            try
            {
                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();
                Item toThrow = swapAction.toThrow;

                // Potentially use GClass2426.Swap instead?

                log.logWarning($"Throwing item: {toThrow.Name.Localized()}");
                inventoryController.ThrowItem(
                    toThrow,
                    null,
                    new Callback(
                        async (IResult result) =>
                        {
                            if (result.Succeed)
                            {
                                await swapAction.callback();
                            }

                            promise.TrySetResult(result);
                        }
                    ),
                    false
                );

                await promise.Task;

                if (swapAction.onComplete != null)
                {
                    await swapAction.onComplete();
                }
            }
            catch (Exception e)
            {
                log.logError(e);
            }
        }
    }
}
