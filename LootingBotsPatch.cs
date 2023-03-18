using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
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
        private static Log log;

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
            itemAdder = new ItemAdder(___botOwner_0, ___gclass263_0, Logger);
            log = new Log(Logger);

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
            log.logDebug(
                $"{itemAdder.botOwner_0.Profile.Info.Settings.Role} is looting corpse: {itemAdder.corpse.Profile?.Info?.Settings?.Role}"
            );

            Item[] priorityItems = itemAdder.corpseInventoryController.Inventory.Equipment
                .GetSlotsByName(
                    new EquipmentSlot[]
                    {
                        EquipmentSlot.Backpack,
                        EquipmentSlot.ArmorVest,
                        EquipmentSlot.TacticalVest,
                        EquipmentSlot.FirstPrimaryWeapon,
                        EquipmentSlot.SecondPrimaryWeapon,
                        EquipmentSlot.Holster,
                        EquipmentSlot.Headwear,
                        EquipmentSlot.Earpiece,
                        EquipmentSlot.Dogtag,
                        EquipmentSlot.Pockets,
                        EquipmentSlot.Scabbard,
                        EquipmentSlot.FaceCover
                    }
                )
                .Select(slot => slot.ContainedItem)
                .ToArray();

            await itemAdder.tryAddItemsToBot(priorityItems);

            // After all equipment looting is done, attempt to change to the bots "main" weapon. Order follows primary -> secondary -> holster
            log.logDebug("Changing to main wep");
            itemAdder.botOwner_0.WeaponManager.Selector.TakeMainWeapon();
        }

        // TODO: When picking up guns, see if you can get them to switch weapons after equipping
        public class ItemAdder
        {
            public Log log;
            public TransactionController transactionController;
            public BotOwner botOwner_0;
            public InventoryControllerClass botInventoryController;
            public Player corpse;
            public InventoryControllerClass corpseInventoryController;

            // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
            public int currentBodyArmorClass = 0;

            public ItemAdder(
                BotOwner botOwner_0,
                GClass263 ___gclass263_0,
                BepInEx.Logging.ManualLogSource Logger
            )
            {
                try
                {
                    this.log = new Log(Logger);

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
                    this.transactionController = new TransactionController(
                        this.botInventoryController,
                        Logger
                    );

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
                        log.logDebug($"Loot found on corpse: {item.Name.Localized()}");
                        // Check to see if we need to swap gear
                        TransactionController.EquipAction action = getEquipAction(item);
                        if (action.swap != null)
                        {
                            await transactionController.throwAndEquip(action.swap);
                            continue;
                        }
                        else if (action.move != null)
                        {
                            await transactionController.moveItem(action.move);
                            continue;
                        }

                        // Check to see if we can equip the item
                        GClass2419 ableToEquip = botInventoryController.FindSlotToPickUp(item);
                        if (ableToEquip != null)
                        {
                            log.logWarning(
                                $"{botOwner_0.Profile.Info.Settings.Role} is equipping: {item.Name.Localized()}"
                            );
                            await transactionController.moveItem(
                                new TransactionController.MoveAction(item, ableToEquip)
                            );
                            continue;
                        }
                        else
                        {
                            log.logDebug($"Cannot equip: {item.Name.Localized()}");
                        }

                        // Check to see if we can pick up the item
                        GClass2421 ableToPickUp = botInventoryController.FindGridToPickUp(
                            item,
                            botInventoryController
                        );

                        if (ableToPickUp != null)
                        {
                            log.logWarning(
                                $"{botOwner_0.Profile.Info.Settings.Role} is picking up: {item.Name.Localized()}"
                            );
                            await transactionController.moveItem(
                                new TransactionController.MoveAction(item, ableToPickUp)
                            );
                            continue;
                        }
                        else
                        {
                            log.logDebug($"No valid slot found for: {item.Name.Localized()}");
                        }

                        // If we cant pick up the item and it has nested slots, loot the items from the container
                        await lootNestedItems(item);
                    }
                    else
                    {
                        log.logDebug("Item was null");
                    }
                }
            }

            /**
            * Checks certain slots to see if the item we are looting is "better" than what is currently equipped. View shouldSwapGear for criteria.
            * Gear is checked in a specific order so that bots will try to swap gear that is a "container" first like backpacks and tacVests to make sure
            * they arent putting loot in an item they will ultimately decide to drop
            */
            public TransactionController.EquipAction getEquipAction(Item lootItem)
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
                TransactionController.EquipAction action = new TransactionController.EquipAction();
                TransactionController.SwapAction swapAction = null;

                if (lootItem is Weapon)
                {
                    return getWeaponEquipAction(lootItem as Weapon);
                }

                if (backpack?.Parent?.Container.ID == lootID && shouldSwapGear(backpack, lootItem))
                {
                    swapAction = getSwapAction(backpack, lootItem, null, true);
                }
                else if (
                    helmet?.Parent?.Container?.ID == lootID && shouldSwapGear(helmet, lootItem)
                )
                {
                    swapAction = getSwapAction(helmet, lootItem);
                }
                else if (chest?.Parent?.Container?.ID == lootID && shouldSwapGear(chest, lootItem))
                {
                    swapAction = getSwapAction(chest, lootItem);
                }
                else if (
                    tacVest?.Parent?.Container?.ID == lootID && shouldSwapGear(tacVest, lootItem)
                )
                {
                    // If the tac vest we are looting is higher armor class and we have a chest equipped, make sure to drop the chest and pick up the armored rig
                    if (isLootingBetterArmor(tacVest, lootItem) && chest != null)
                    {
                        log.logDebug("Bot looting armored rig and dropping chest");
                        swapAction = getSwapAction(
                            chest,
                            null,
                            async () =>
                                await transactionController.throwAndEquip(
                                    getSwapAction(tacVest, lootItem, null, true)
                                )
                        );
                    }
                    else
                    {
                        swapAction = getSwapAction(tacVest, lootItem, null, true);
                    }
                }

                action.swap = swapAction;
                return action;
            }

            public TransactionController.EquipAction getWeaponEquipAction(Weapon lootWeapon)
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

                TransactionController.EquipAction action = new TransactionController.EquipAction();
                bool isPistol = lootWeapon.WeapClass.Equals("pistol");

                if (isPistol && holster != null)
                {
                    log.logDebug(
                        $"Trying to swap {holster.Name.Localized()} with {lootWeapon.Name.Localized()}"
                    );
                    action.swap = getSwapAction(holster, lootWeapon);
                }
                else if (!isPistol && primary != null)
                {
                    if (secondary == null)
                    {
                        ItemAddress canEquipToSecondary = botInventoryController.FindSlotToPickUp(
                            primary
                        );
                        action.move = new TransactionController.MoveAction(
                            primary,
                            canEquipToSecondary,
                            async () =>
                            {
                                // Delay to wait for animation to complete. Bot animation is playing for putting the primary weapon away
                                await Task.Delay(1000);
                                await tryAddItemsToBot(new Item[] { lootWeapon });
                            }
                        );
                        log.logDebug(
                            $"Trying to move {primary.Name.Localized()} to secondary slot and equip {lootWeapon.Name.Localized()}"
                        );
                    }
                    else
                    {
                        // TODO: Swap weapon with weapon slot that has the worst value
                        log.logDebug(
                            $"Trying to swap {primary.Name.Localized()} with {lootWeapon.Name.Localized()}"
                        );
                        action.swap = getSwapAction(primary, lootWeapon);
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

            /** Searches throught the child items of a container and attempts to loot them */
            public async Task lootNestedItems(Item parentItem)
            {
                Item[] nestedItems = parentItem.GetAllItems().ToArray();
                if (nestedItems.Length > 1)
                {
                    log.logDebug(
                        $"looting nested {nestedItems.Length} items from {parentItem.Name.Localized()}"
                    );
                    Item[] containerItems = nestedItems
                        .Where(nestedItem => nestedItem.Id != parentItem.Id)
                        .ToArray();

                    if (containerItems.Length > 0)
                    {
                        await tryAddItemsToBot(containerItems);
                    }
                }
            }

            /** Generates a SwapAction to send to the transaction controller*/
            public TransactionController.SwapAction getSwapAction(
                Item toThrow,
                Item toEquip,
                TransactionController.ActionCallback callback = null,
                bool tranferItems = false
            )
            {
                TransactionController.ActionCallback onSwapComplete = null;
                // If we want to transfer items after the throw and equip fully completes, call the lootNestedItems method 
                // on the item that was just thrown
                if (tranferItems)
                {
                    onSwapComplete = async () =>
                    {
                        await lootNestedItems(toThrow);
                    };
                }

                return new TransactionController.SwapAction(
                    toThrow,
                    toEquip,
                    callback != null ? callback : async () =>
                    {   
                        // Try to equip the item after throwing
                        await tryAddItemsToBot(new Item[1] { toEquip });
                    },
                    onSwapComplete
                );
            }
        }
    }
}
