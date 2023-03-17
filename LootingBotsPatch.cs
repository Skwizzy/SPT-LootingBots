using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using EFT.InventoryLogic;
using Comfort.Common;
using EFT;

namespace LootingBots.Patch
{
    /** For Debugging */
    public class PickupActionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass452).GetMethod(
                "PickupAction",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            Player owner,
            GInterface264 possibleAction,
            Item rootItem,
            Player lootItemLastOwner
        )
        {
            Logger.LogDebug($"Someone is picking up: {rootItem.Name.Localized()}");
            return true;
        }
    }

    public class LootCorpsePatch : ModulePatch
    {
        private static MethodInfo _method_7;
        private static ItemAdder itemAdder;

        public static bool isDebug()
        {
            return LootingBots.enableLogging.Value;
        }

        public static void logDebug(object data)
        {
            if (isDebug())
            {
                Logger.LogDebug(data);
            }
        }

        public static void logWarning(object data)
        {
            if (isDebug())
            {
                Logger.LogWarning(data);
            }
        }

        protected override MethodBase GetTargetMethod()
        {
            _method_7 = typeof(GClass325).GetMethod(
                "method_7",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            return typeof(GClass325).GetMethod(
                "method_6",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref GClass325 __instance,
            ref BotOwner ___botOwner_0,
            ref GClass263 ___gclass263_0
        )
        {
            itemAdder = new ItemAdder(___botOwner_0, ___gclass263_0);

            try
            {
                lootCorpse();
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }

        public static async void lootCorpse()
        {
            logDebug(
                $"{itemAdder.botOwner_0.Profile.Info.Settings.Role} is looting corpse: {itemAdder.corpse.Profile?.Info?.Settings?.Role}"
            );

            Item[] priorityItems = itemAdder.corpseInventoryController.Inventory.Equipment
                .GetSlotsByName(
                    new EquipmentSlot[]
                    {
                        EquipmentSlot.Backpack,
                        EquipmentSlot.ArmorVest,
                        EquipmentSlot.TacticalVest,
                        EquipmentSlot.Headwear,
                        EquipmentSlot.Earpiece,
                        EquipmentSlot.FirstPrimaryWeapon,
                        EquipmentSlot.SecondPrimaryWeapon,
                        EquipmentSlot.Holster,
                        EquipmentSlot.Dogtag,
                        EquipmentSlot.Pockets,
                        EquipmentSlot.Scabbard,
                        EquipmentSlot.FaceCover
                    }
                )
                .Select(slot => slot.ContainedItem)
                .ToArray();

            await itemAdder.tryAddItemsToBot(priorityItems);
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
            public bool tranferItems = false;

            public SwapAction(
                Item toThrow = null,
                Item toEquip = null,
                ActionCallback callback = null,
                bool tranferItems = false
            )
            {
                this.toThrow = toThrow;
                this.toEquip = toEquip;
                this.callback = callback;
                this.tranferItems = tranferItems;
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

        // TODO: When picking up guns, see if you can get them to switch weapons after equipping
        public class ItemAdder
        {
            public BotOwner botOwner_0;
            public InventoryControllerClass botInventoryController;
            public Player corpse;
            public InventoryControllerClass corpseInventoryController;

            // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
            public int currentBodyArmorClass = 0;

            public ItemAdder(BotOwner botOwner_0, GClass263 ___gclass263_0)
            {
                try
                {
                    // Initialize bot inventory controller
                    Type botOwnerType = botOwner_0.GetPlayer.GetType();
                    FieldInfo botInventory = botOwnerType.BaseType.GetField(
                        "_inventoryController",
                        BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Public
                            | BindingFlags.Instance
                    );

                    this.botOwner_0 = botOwner_0;
                    this.botInventoryController = (InventoryControllerClass)
                        botInventory.GetValue(botOwner_0.GetPlayer);

                    // Initialize corpse inventory controller
                    Player corpse = ___gclass263_0.Player;
                    Type corpseType = corpse.GetType();
                    FieldInfo corpseInventory = corpseType.BaseType.GetField(
                        "_inventoryController",
                        BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Public
                            | BindingFlags.Instance
                    );
                    this.corpse = corpse;
                    this.corpseInventoryController = (InventoryControllerClass)
                        corpseInventory.GetValue(corpse);

                    Item chest = this.botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.ArmorVest)
                        .ContainedItem;
                    Item tacVest = this.botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.TacticalVest)
                        .ContainedItem;
                    ArmorComponent currentArmor = chest?.GetItemComponent<ArmorComponent>();
                    ArmorComponent currentVest = tacVest?.GetItemComponent<ArmorComponent>();

                    this.currentBodyArmorClass =
                        currentArmor?.ArmorClass ?? currentVest?.ArmorClass ?? 0;
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }

            /**
            * Main driving method which kicks off the logic for what a bot will do with the loot found on a corpse.
            * If bots are looting something that is equippable and they have nothing equipped in that slot, they will always equip it.
            * If the bot decides not to equip the item then it will attempt to put in an available container slot
            */
            public async Task tryAddItemsToBot(Item[] items)
            {
                foreach (Item item in items)
                {
                    if (item != null && item.Name != null)
                    {
                        logDebug($"Loot found on corpse: {item.Name.Localized()}");
                        // Check to see if we need to swap gear
                        EquipAction action = getEquipAction(item);
                        if (action.swap != null)
                        {
                            await throwAndEquip(action.swap);
                            continue;
                        }
                        else if (action.move != null)
                        {
                            await moveItem(action.move);
                            continue;
                        }

                        // Check to see if we can equip the item
                        GClass2419 ableToEquip = botInventoryController.FindSlotToPickUp(item);
                        if (ableToEquip != null)
                        {
                            logWarning(
                                $"{botOwner_0.Profile.Info.Settings.Role} is equipping: {item.Name.Localized()}"
                            );
                            await moveItem(new MoveAction(item, ableToEquip));
                            continue;
                        }
                        else
                        {
                            logDebug($"Cannot equip: {item.Name.Localized()}");
                        }

                        // Check to see if we can pick up the item
                        GClass2421 ableToPickUp = botInventoryController.FindGridToPickUp(
                            item,
                            botInventoryController
                        );

                        if (ableToPickUp != null)
                        {
                            logWarning(
                                $"{botOwner_0.Profile.Info.Settings.Role} is picking up: {item.Name.Localized()}"
                            );
                            await moveItem(new MoveAction(item, ableToPickUp));
                            continue;
                        }
                        else
                        {
                            logDebug($"No valid slot found for: {item.Name.Localized()}");
                        }

                        // If we cant pick up the item and it has nested slots, loot the items from the container
                        await lootNestedItems(item);
                    }
                    else
                    {
                        logDebug("Item was null");
                    }
                }
            }

            /**
            * Checks certain slots to see if the item we are looting is "better" than what is currently equipped. View shouldSwapGear for criteria.
            * Gear is checked in a specific order so that bots will try to swap gear that is a "container" first like backpacks and tacVests to make sure
            * they arent putting loot in an item they will ultimately decide to drop
            */
            public EquipAction getEquipAction(Item lootItem)
            {
                // TODO: Try to combine this into one call to get all 4 slots
                Item helmet = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Headwear)
                    .ContainedItem;
                Item chest = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.ArmorVest)
                    .ContainedItem;
                Item tacVest = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem;
                Item backpack = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Backpack)
                    .ContainedItem;

                string lootID = lootItem?.Parent?.Container?.ID;
                EquipAction action = new EquipAction();
                SwapAction swapAction = null;

                if (lootItem is Weapon)
                {
                    return getWeaponEquipAction(lootItem as Weapon);
                }

                if (backpack?.Parent?.Container.ID == lootID && shouldSwapGear(backpack, lootItem))
                {
                    swapAction = new SwapAction(backpack, lootItem, null, true);
                }
                else if (
                    helmet?.Parent?.Container?.ID == lootID && shouldSwapGear(helmet, lootItem)
                )
                {
                    swapAction = new SwapAction(helmet, lootItem);
                }
                else if (chest?.Parent?.Container?.ID == lootID && shouldSwapGear(chest, lootItem))
                {
                    swapAction = new SwapAction(chest, lootItem);
                }
                else if (
                    tacVest?.Parent?.Container?.ID == lootID && shouldSwapGear(tacVest, lootItem)
                )
                {
                    // If the tac vest we are looting is higher armor class and we have a chest equipped, make sure to drop the chest and pick up the armored rig
                    if (isLootingBetterArmor(tacVest, lootItem) && chest != null)
                    {
                        logDebug("Bot looting armored rig and dropping chest");
                        swapAction = new SwapAction(
                            chest,
                            null,
                            async () =>
                                await throwAndEquip(new SwapAction(tacVest, lootItem, null, true))
                        );
                    }
                    else
                    {
                        swapAction = new SwapAction(tacVest, lootItem, null, true);
                    }
                }

                action.swap = swapAction;
                return action;
            }

            public EquipAction getWeaponEquipAction(Weapon lootWeapon)
            {
                Item primary = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.FirstPrimaryWeapon)
                    .ContainedItem;
                Item secondary = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.SecondPrimaryWeapon)
                    .ContainedItem;
                Item holster = botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Holster)
                    .ContainedItem;

                EquipAction action = new EquipAction();
                bool isPistol = lootWeapon.WeapClass.Equals("pistol");

                if (isPistol && holster != null)
                {
                    logDebug(
                        $"Trying to swap {holster.Name.Localized()} with {lootWeapon.Name.Localized()}"
                    );
                    action.swap = new SwapAction(holster, lootWeapon);
                }
                else if (!isPistol && primary != null)
                {
                    if (secondary == null)
                    {
                        ItemAddress canEquipToSecondary = botInventoryController.FindSlotToPickUp(
                            primary
                        );
                        action.move = new MoveAction(
                            primary,
                            canEquipToSecondary,
                            async () =>
                            {
                                await Task.Delay(1500);
                                await tryAddItemsToBot(new Item[] { lootWeapon });
                                await Task.Delay(1000);
                                botOwner_0.WeaponManager.Selector.TryChangeToMain();
                            }
                        );
                        logDebug(
                            $"Trying to move {primary.Name.Localized()} to secondary slot and equip {lootWeapon.Name.Localized()}"
                        );
                    }
                    else
                    {
                        // TODO: Swap weapon with weapon slot that has the worst value
                        logDebug(
                            $"Trying to swap {primary.Name.Localized()} with {lootWeapon.Name.Localized()}"
                        );
                        action.swap = new SwapAction(primary, lootWeapon);
                    }
                }

                return action;
            }

            /**
            * Checks to see if the bot should swap its currently equipped gear with the item to loot. Bot will swap under the following criteria:
            * 1. The item is a container and its larger than what is equipped.
            *   - Tactical rigs have an additional check, will not switch out if the rig we are looting is lower armor class than what is equipped
            * 2. The item has an armor rating, and its higher than what is currently equipped.
            */
            public bool shouldSwapGear(Item equipped, Item itemToLoot)
            {
                bool foundBiggerContainer = false;
                // If the item is a container, calculate the size and see if its bigger than what is equipped
                if (equipped.IsContainer)
                {
                    int equippedSize = getContainerSize(equipped as SearchableItemClass);
                    int itemToLootSize = getContainerSize(itemToLoot as SearchableItemClass);

                    foundBiggerContainer = equippedSize < itemToLootSize;
                }

                bool foundBetterArmor = isLootingBetterArmor(equipped, itemToLoot);
                ArmorComponent lootArmor = itemToLoot.GetItemComponent<ArmorComponent>();
                ArmorComponent equippedArmor = equipped.GetItemComponent<ArmorComponent>();

                // Equip if we found item with a better item class.
                // Equip if we found an item with more slots only if what we have equipped is the same or worse armor class
                return foundBetterArmor
                    || (
                        foundBiggerContainer
                        && (
                            equippedArmor == null
                            || equippedArmor.ArmorClass <= lootArmor.ArmorClass
                        )
                    );
            }

            /** Calculate the size of a container */
            public int getContainerSize(SearchableItemClass container)
            {
                GClass2163[] grids = container.Grids;
                int gridSize = 0;

                foreach (GClass2163 grid in grids)
                {
                    gridSize += grid.GridHeight.Value * grid.GridWidth.Value;
                }

                return gridSize;
            }

            /**
            * Checks to see if the item we are looting has higher armor value than what is currently equipped. For chests/vests, make sure we compare against the
            * currentBodyArmorClass and update the value if a higher armor class is found.
            */
            public bool isLootingBetterArmor(Item equipped, Item itemToLoot)
            {
                ArmorComponent lootArmor = itemToLoot.GetItemComponent<ArmorComponent>();
                HelmetComponent lootHelmet = itemToLoot.GetItemComponent<HelmetComponent>();
                ArmorComponent equippedArmor = equipped.GetItemComponent<ArmorComponent>();

                // If the equipped item is not an ArmorComponent then assume the lootArmor item is higher class
                if (equippedArmor == null)
                {
                    return lootArmor != null;
                }

                bool foundBetterArmor = false;

                // If we are looting a helmet, check to see if it has a better armor class than what is equipped
                if (lootArmor != null && lootHelmet != null)
                {
                    foundBetterArmor = equippedArmor.ArmorClass <= lootArmor.ArmorClass;
                }
                else if (lootArmor != null)
                {
                    // If we are looting chest/rig with armor, check to see if it has a better armor class than what is equipped
                    foundBetterArmor = currentBodyArmorClass <= lootArmor.ArmorClass;

                    if (foundBetterArmor)
                    {
                        this.currentBodyArmorClass = lootArmor.ArmorClass;
                    }
                }

                return foundBetterArmor;
            }

            public async Task lootNestedItems(Item parentItem)
            {
                Item[] nestedItems = parentItem.GetAllItems().ToArray();
                if (nestedItems.Length > 1)
                {
                    Item[] containerItems = nestedItems
                        .Where(nestedItem => nestedItem.Id != parentItem.Id)
                        .ToArray();

                    if (containerItems.Length > 0)
                    {
                        await tryAddItemsToBot(containerItems);
                    }
                }
            }

            /** Method used when we want the bot the throw an item and then equip an item immidiately afterwards */
            public async Task throwAndEquip(SwapAction swapAction)
            {
                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();
                Item toThrow = swapAction.toThrow;

                // Potentially use GClass2426.Swap instead?

                logWarning($"Throwing item: {toThrow}");

                botInventoryController.ThrowItem(
                    toThrow,
                    null,
                    new Callback(
                        async (IResult result) =>
                        {
                            // If the swap action has a custom callback, do not execute the default behavior
                            if (swapAction.callback != null)
                            {
                                await swapAction.callback();
                            }
                            else
                            {
                                // Try to equip the item after throwing
                                await tryAddItemsToBot(new Item[1] { swapAction.toEquip });
                                if (toThrow is Weapon)
                                {
                                    if (
                                        toThrow.Parent.Container.ID.ToLower()
                                        == "firstprimaryweapon"
                                    )
                                    {
                                        botOwner_0.WeaponManager.Selector.ChangeToMain();
                                    }
                                }
                            }
                            promise.TrySetResult(result);
                        }
                    ),
                    false
                );

                await Task.WhenAny(promise.Task);
                
                // If marked for transfer of items, recover any items from the thrown container
                if (swapAction.tranferItems)
                {
                    await lootNestedItems(toThrow);
                }
            }

            /** Moves an item to a specified item address. Supports executing a callback */
            public Task moveItem(MoveAction moveAction)
            {
                // GClass2426.
                logDebug($"Moving item to: {moveAction.place.Container?.ID}");
                GStruct322<GClass2438> value = GClass2426.Move(
                    moveAction.toMove,
                    moveAction.place,
                    botInventoryController,
                    true
                );

                if (moveAction.callback == null)
                {
                    return botInventoryController.TryRunNetworkTransaction(value, null);
                }

                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

                botInventoryController.TryRunNetworkTransaction(
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



        }
    }
}
