using EFT.InventoryLogic;
using System.Threading.Tasks;
using Comfort.Common;

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

        /** Moves an item to a specified item address. Supports executing a callback */
        public async Task moveItem(MoveAction moveAction)
        {
            // GClass2426.
            log.logDebug($"Moving item to: {moveAction.place.Container.ID}");
            GStruct322<GClass2438> value = GClass2426.Move(
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

                await Task.WhenAny(promise.Task);
            }

            if (moveAction.onComplete != null)
            {
                await moveAction.onComplete();
            }
        }

        /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
        public async Task throwAndEquip(SwapAction swapAction)
        {
            TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();
            Item toThrow = swapAction.toThrow;

            // Potentially use GClass2426.Swap instead?

            log.logWarning($"Throwing item: {toThrow}");

            inventoryController.ThrowItem(
                toThrow,
                null,
                new Callback(
                    async (IResult result) =>
                    {
                        await swapAction.callback();
                        promise.TrySetResult(result);
                    }
                ),
                false
            );

            await Task.WhenAny(promise.Task);

            if (swapAction.onComplete != null)
            {
                await swapAction.onComplete();
            }
        }
    }
}
