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

            public SwapAction(
                Item toThrow = null,
                Item toEquip = null,
                ActionCallback callback = null
            )
            {
                this.toThrow = toThrow;
                this.toEquip = toEquip;
                this.callback = callback;
            }
        }

        public class MoveAction
        {
            public Item toMove;
            public ItemAddress place;
            public ActionCallback callback;

            public MoveAction(
                Item toMove = null,
                ItemAddress place = null,
                ActionCallback callback = null
            )
            {
                this.toMove = toMove;
                this.place = place;
                this.callback = callback;
            }
        }

        public delegate Task ActionCallback();

        /** Moves an item to a specified item address. Supports executing a callback */
        public Task moveItem(MoveAction moveAction)
        {
            // GClass2426.
            log.logDebug($"Moving item to: {moveAction.place.Item.Name.Localized()}");
            GStruct322<GClass2438> value = GClass2426.Move(
                moveAction.toMove,
                moveAction.place,
                inventoryController,
                true
            );

            if (moveAction.callback == null)
            {
                return inventoryController.TryRunNetworkTransaction(value, null);
            }

            TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

            inventoryController.TryRunNetworkTransaction(
                value,
                new Callback(
                    async (IResult result) =>
                    {
                        await moveAction.callback();
                        promise.TrySetResult(result);
                    }
                )
            );

            return Task.WhenAny(promise.Task);
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
        }
    }
}
