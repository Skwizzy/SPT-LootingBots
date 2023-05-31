using System;
using System.Linq;
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

        /** Tries to add extra spare ammo for the weapon being looted into the bot's secure container so that the bots are able to refill their mags properly in their reload logic */
        public bool AddExtraAmmo(Weapon weapon)
        {
            try
            {
                SearchableItemClass secureContainer = (SearchableItemClass)
                    _inventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.SecuredContainer)
                        .ContainedItem;

                // Try to get the current ammo used by the weapon by checking the contents of the magazine. If its empty, try to create an instance of the ammo using the Weapon's CurrentAmmoTemplate
                Item ammoToAdd =
                    weapon.GetCurrentMagazine()?.FirstRealAmmo()
                    ?? Singleton<ItemFactory>.Instance.CreateItem(
                        new MongoID(false),
                        weapon.CurrentAmmoTemplate._id,
                        null
                    );

                // Check to see if there already is ammo that meets the weapon's caliber in the secure container
                bool alreadyHasAmmo =
                    secureContainer
                        .GetAllItems()
                        .Where(
                            item =>
                                item is BulletClass bullet
                                && bullet.Caliber.Equals(((BulletClass)ammoToAdd).Caliber)
                        )
                        .ToArray()
                        .Length > 0;

                // If we dont have any ammo, attempt to add 10 max ammo stacks into the bot's secure container for use in the bot's internal reloading code
                if (!alreadyHasAmmo)
                {
                    _log.LogDebug($"Trying to add ammo");
                    int ammoAdded = 0;

                    for (int i = 0; i < 10; i++)
                    {
                        Item ammo = ammoToAdd.CloneItem();
                        ammo.StackObjectsCount = ammo.StackMaxSize;

                        string[] visitorIds = new string[] { _inventoryController.ID };

                        var location = _inventoryController.FindGridToPickUp(
                            ammo,
                            secureContainer.Grids
                        );

                        if (location != null)
                        {
                            var result = location.AddWithoutRestrictions(ammo, visitorIds);
                            if (result.Failed)
                            {
                                _log.LogError(
                                    $"Failed to add {ammo.Name.Localized()} to secure container"
                                );
                            }
                            else
                            {
                                ammoAdded += ammo.StackObjectsCount;
                            }
                        }
                        else
                        {
                            _log.LogError(
                                $"Cannot find location in secure container for {ammo.Name.Localized()}"
                            );
                        }
                    }

                    if (ammoAdded > 0)
                    {
                        _log.LogDebug(
                            $"Successfully added {ammoAdded} round of {ammoToAdd.Name.Localized()}"
                        );
                    }
                }
                else
                {
                    _log.LogDebug($"Already has ammo for {weapon.Name.Localized()}");
                }

                return true;
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }

            return false;
        }

        /** Tries to find an open Slot to equip the current item to. If a slot is found, issue a move action to equip the item */
        public async Task<bool> TryEquipItem(Item item)
        {
            try
            {
                string botName = $"({_botOwner.Profile.Info.Settings.Role}) Bot {_botOwner.Id}";

                // Check to see if we can equip the item
                var ableToEquip = _inventoryController.FindSlotToPickUp(item);
                if (ableToEquip != null)
                {
                    _log.LogWarning(
                        $"{botName} is equipping: {item.Name.Localized()} [place: {ableToEquip.Container.ID.Localized()}]"
                    );
                    bool success = await MoveItem(new MoveAction(item, ableToEquip));
                    return success;
                }

                _log.LogDebug($"Cannot equip: {item.Name.Localized()}");
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }

            return false;
        }

        /** Tries to find a valid grid for the item being looted. Checks all containers currently equipped to the bot. If there is a valid grid to place the item inside of, issue a move action to pick up the item */
        public async Task<bool> TryPickupItem(Item item)
        {
            try
            {
                string botName = $"({_botOwner.Profile.Info.Settings.Role}) Bot {_botOwner.Id}";
                var ableToPickUp = _inventoryController.FindGridToPickUp(item);

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
                    return await MoveItem(new MoveAction(item, ableToPickUp));
                }

                _log.LogDebug($"No valid slot found for: {item.Name.Localized()}");
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
            return false;
        }

        /** Moves an item to a specified item address. Supports executing a callback */
        public async Task<bool> MoveItem(MoveAction moveAction)
        {
            try
            {
                if (IsLootingInterrupted(_botOwner))
                {
                    return false;
                }

                if (moveAction.ToMove is Weapon weapon && !(moveAction.ToMove is BulletClass))
                {
                    AddExtraAmmo(weapon);
                }

                _log.LogDebug($"Moving item to: {moveAction?.Place?.Container?.ID?.Localized()}");
                var value = GClass2429.Move(
                    moveAction.ToMove,
                    moveAction.Place,
                    _inventoryController,
                    true
                );

                if (value.Failed)
                {
                    _log.LogError(
                        $"Failed to move {moveAction.ToMove.Name.Localized()} to {moveAction.Place.Container.ID.Localized()}"
                    );
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
                _log.LogError(e);
            }

            return true;
        }

        /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
        public async Task<bool> ThrowAndEquip(SwapAction swapAction)
        {
            if (IsLootingInterrupted(_botOwner))
            {
                return false;
            }

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
                                if (swapAction.Callback != null)
                                {
                                    await SimulatePlayerDelay();
                                    await swapAction.Callback();
                                }
                            }

                            promise.TrySetResult(result);
                        }
                    ),
                    false
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
                _log.LogError(e);
            }

            return false;
        }

        public Task<IResult> TryRunNetworkTransaction(
            GStruct324 operationResult,
            Callback callback = null
        )
        {
            return _inventoryController.TryRunNetworkTransaction(operationResult, callback);
        }

        public static Task SimulatePlayerDelay(int delay = 500)
        {
            return Task.Delay(delay);
        }

        /*
            Check the bot Brain to see if the last decision it made was related to looting, otherwise this is an indicator the the bot is not supposed to be looting as was interrupted
            due to combat, death, ect
        */
        public static bool IsLootingInterrupted(BotOwner botOwner)
        {
            if (
                botOwner == null
                || botOwner.Brain == null
                || (
                    botOwner.Brain.LastDecision != BotLogicDecision.deadBody
                    && botOwner.Brain.LastDecision != BotLogicDecision.botTakeItem
                )
            )
            {
                LootingBots.LootLog.LogWarning($"Bot {botOwner?.Id} was interrupted looting!");
                return true;
            }

            return false;
        }
    }
}
