using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT;
using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class GearValue
    {
        public ValuePair primary = new ValuePair("", 0);
        public ValuePair secondary = new ValuePair("", 0);
        public ValuePair holster = new ValuePair("", 0);
    }

    public class ValuePair
    {
        public string Id;
        public float value = 0;

        public ValuePair(string Id, float value)
        {
            this.Id = Id;
            this.value = value;
        }
    }

    public class ItemAdder
    {
        public Log log;
        public TransactionController transactionController;
        public BotOwner botOwner;
        public InventoryControllerClass botInventoryController;

        private static ItemAppraiser itemAppraiser;

        private static GearValue gearValue = new GearValue();

        // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
        public int currentBodyArmorClass = 0;

        public ItemAdder(BotOwner botOwner)
        {
            try
            {
                this.log = LootingBots.lootLog;
                itemAppraiser = LootingBots.itemAppraiser;

                // Initialize bot inventory controller
                Type botOwnerType = botOwner.GetPlayer.GetType();
                FieldInfo botInventory = botOwnerType.BaseType.GetField(
                    "_inventoryController",
                    BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.Public
                        | BindingFlags.Instance
                );

                this.botOwner = botOwner;
                this.botInventoryController = (InventoryControllerClass)
                    botInventory.GetValue(botOwner.GetPlayer);
                this.transactionController = new TransactionController(
                    this.botOwner,
                    this.botInventoryController,
                    this.log
                );

                // Initialize current armor classs
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

                calculateGearValue();
            }
            catch (Exception e)
            {
                log.logError(e);
            }
        }

        public void calculateGearValue()
        {
            log.logDebug("Calculating gear value...");
            Item primary = botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.FirstPrimaryWeapon)
                .ContainedItem;
            Item secondary = botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.SecondPrimaryWeapon)
                .ContainedItem;
            Item holster = botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.Holster)
                .ContainedItem;

            if (primary != null && gearValue.primary.Id != primary.Id)
            {
                float value = itemAppraiser.getItemPrice(primary);
                gearValue.primary = new ValuePair(primary.Id, value);
            }
            if (secondary != null && gearValue.secondary.Id != secondary.Id)
            {
                float value = itemAppraiser.getItemPrice(secondary);
                gearValue.secondary = new ValuePair(secondary.Id, value);
            }
            if (holster != null && gearValue.holster.Id != holster.Id)
            {
                float value = itemAppraiser.getItemPrice(holster);
                gearValue.holster = new ValuePair(holster.Id, value);
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
                    log.logDebug($"Loot found: {item.Name.Localized()}");
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
                    bool ableToEquip = await transactionController.tryEquipItem(item);

                    if (ableToEquip)
                    {
                        continue;
                    }

                    // Check to see if we can pick up the item
                    bool ableToPickUp = await transactionController.tryPickupItem(item);

                    if (ableToPickUp)
                    {
                        continue;
                    }

                    // If we cant pick up the item and it has nested slots, loot the items from the container
                    await lootNestedItems(item);
                }
                else
                {
                    log.logDebug("Item was null");
                }
            }

            botOwner.WeaponManager.Selector.TakeMainWeapon();
        }

        /**
        * Checks certain slots to see if the item we are looting is "better" than what is currently equipped. View shouldSwapGear for criteria.
        * Gear is checked in a specific order so that bots will try to swap gear that is a "container" first like backpacks and tacVests to make sure
        * they arent putting loot in an item they will ultimately decide to drop
        */
        public TransactionController.EquipAction getEquipAction(Item lootItem)
        {
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

            if ((lootItem.Template is WeaponTemplate) && !((Weapon)lootItem).IsFlareGun)
            {
                return getWeaponEquipAction(lootItem as Weapon);
            }

            if (backpack?.Parent?.Container.ID == lootID && shouldSwapGear(backpack, lootItem))
            {
                swapAction = getSwapAction(backpack, lootItem, null, true);
            }
            else if (helmet?.Parent?.Container?.ID == lootID && shouldSwapGear(helmet, lootItem))
            {
                swapAction = getSwapAction(helmet, lootItem);
            }
            else if (chest?.Parent?.Container?.ID == lootID && shouldSwapGear(chest, lootItem))
            {
                swapAction = getSwapAction(chest, lootItem);
            }
            else if (tacVest?.Parent?.Container?.ID == lootID && shouldSwapGear(tacVest, lootItem))
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

            float lootValue = itemAppraiser.getItemPrice(lootWeapon);

            if (isPistol)
            {
                if (holster == null)
                {
                    action.move = new TransactionController.MoveAction(
                        lootWeapon,
                        botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    gearValue.holster = new ValuePair(lootWeapon.Id, lootValue);
                }
                else if (holster != null && gearValue.holster.value < lootValue)
                {
                    log.logDebug(
                        $"Trying to swap {holster.Name.Localized()} (₽{gearValue.holster.value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                    );
                    action.swap = getSwapAction(holster, lootWeapon);
                    gearValue.holster = new ValuePair(lootWeapon.Id, lootValue);
                }
            }
            else
            {
                // If we have no primary, just equip the weapon to primary
                if (primary == null)
                {
                    action.move = new TransactionController.MoveAction(
                        lootWeapon,
                        botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    gearValue.primary = new ValuePair(lootWeapon.Id, lootValue);
                }
                else if (gearValue.primary.value < lootValue)
                {
                    // If the loot weapon is worth more than the primary, by nature its also worth more than the secondary. Try to move the primary weapon to the secondary slot and equip the new weapon as the primary
                    if (secondary == null)
                    {
                        ItemAddress canEquipToSecondary = botInventoryController.FindSlotToPickUp(
                            primary
                        );
                        log.logDebug(
                            $"Moving {primary.Name.Localized()} (₽{gearValue.primary.value}) to secondary and equipping {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );
                        action.move = new TransactionController.MoveAction(
                            primary,
                            canEquipToSecondary,
                            null,
                            async () =>
                            {
                                // Delay to wait for animation to complete. Bot animation is playing for putting the primary weapon away
                                await Task.Delay(1000);
                                await transactionController.tryEquipItem(lootWeapon);
                            }
                        );

                        gearValue.secondary = gearValue.primary;
                        gearValue.primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                    // In the case where we have a secondary, throw it, move the primary to secondary, and equip the loot weapon as primary
                    else
                    {
                        log.logDebug(
                            $"Trying to swap {secondary.Name.Localized()} (₽{gearValue.secondary.value}) with {primary.Name.Localized()} (₽{gearValue.primary.value}) and equip {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );
                        action.swap = getSwapAction(
                            secondary,
                            primary,
                            null,
                            false,
                            async () =>
                            {
                                await Task.Delay(1000);
                                await transactionController.tryEquipItem(lootWeapon);
                            }
                        );
                        gearValue.secondary = gearValue.primary;
                        gearValue.primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                // If there is no secondary weapon, equip to secondary
                else if (secondary == null)
                {
                    action.move = new TransactionController.MoveAction(
                        lootWeapon,
                        botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    gearValue.secondary = new ValuePair(lootWeapon.Id, lootValue);
                }
                // If the loot weapon is worth more than the secondary, swap it
                else if (gearValue.secondary.value < lootValue)
                {
                    log.logDebug(
                        $"Trying to swap {secondary.Name.Localized()} (₽{gearValue.secondary.value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                    );
                    action.swap = getSwapAction(secondary, lootWeapon);
                    gearValue.secondary = new ValuePair(secondary.Id, lootValue);
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

            // Equip if we found item with a better armor class.
            // Equip if we found an item with more slots only if what we have equipped is the same or worse armor class
            return foundBetterArmor
                || (
                    foundBiggerContainer
                    && (equippedArmor == null || equippedArmor.ArmorClass <= lootArmor?.ArmorClass)
                );
        }

        /** Calculate the size of a container */
        public int getContainerSize(SearchableItemClass container)
        {
            GClass2166[] grids = container.Grids;
            int gridSize = 0;

            foreach (GClass2166 grid in grids)
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

            bool foundBetterArmor = false;

            // If we are looting a helmet, check to see if it has a better armor class than what is equipped
            if (lootArmor != null && lootHelmet != null)
            {
                // If the equipped item is not an ArmorComponent then assume the lootArmor item is higher class
                if (equippedArmor == null)
                {
                    return lootArmor != null;
                }

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
                // Filter out the parent item from the list, filter out any items that are children of another container like a magazine, backpack, rig
                Item[] containerItems = nestedItems
                    .Where(
                        nestedItem =>
                            nestedItem.Id != parentItem.Id
                            && nestedItem.Id == nestedItem.GetRootItem().Id
                            && !nestedItem.QuestItem
                            && !isSingleUseKey(nestedItem)
                    )
                    .ToArray();

                if (containerItems.Length > 0)
                {
                    log.logDebug(
                        $"Looting {containerItems.Length} items from {parentItem.Name.Localized()}"
                    );
                    await tryAddItemsToBot(containerItems);
                }
            }
            else
            {
                log.logDebug($"No nested items found in {parentItem.Name}");
            }
        }

        // Prevents bots from looting single use quest keys like "Unknown Key"
        public bool isSingleUseKey(Item item)
        {
            KeyComponent key = item.GetItemComponent<KeyComponent>();
            return key != null && key.Template.MaximumNumberOfUsage == 1;
        }

        /** Generates a SwapAction to send to the transaction controller*/
        public TransactionController.SwapAction getSwapAction(
            Item toThrow,
            Item toEquip,
            TransactionController.ActionCallback callback = null,
            bool tranferItems = false,
            TransactionController.ActionCallback onComplete = null
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
                callback != null
                    ? callback
                    : async () =>
                    {
                        await Task.Delay(1000);
                        // Try to equip the item after throwing
                        await transactionController.tryEquipItem(toEquip);
                    },
                onComplete != null ? onComplete : onSwapComplete
            );
        }
    }
}
