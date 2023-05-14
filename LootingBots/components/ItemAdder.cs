using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Comfort.Common;

using EFT;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class GearValue
    {
        public ValuePair Primary = new ValuePair("", 0);
        public ValuePair Secondary = new ValuePair("", 0);
        public ValuePair Holster = new ValuePair("", 0);
    }

    public class ValuePair
    {
        public string Id;
        public float Value = 0;

        public ValuePair(string id, float value)
        {
            Id = id;
            Value = value;
        }
    }

    public class ItemAdder
    {
        private readonly Log _log;
        private readonly TransactionController _transactionController;
        private readonly BotOwner _botOwner;
        private readonly InventoryControllerClass _botInventoryController;

        private static readonly GearValue GearValue = new GearValue();

        private Dictionary<string, List<MagazineClass>> usableMags =
            new Dictionary<string, List<MagazineClass>>();

        // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
        public int CurrentBodyArmorClass = 0;

        public ItemAdder(BotOwner botOwner)
        {
            try
            {
                _log = LootingBots.LootLog;

                // Initialize bot inventory controller
                Type botOwnerType = botOwner.GetPlayer.GetType();
                FieldInfo botInventory = botOwnerType.BaseType.GetField(
                    "_inventoryController",
                    BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.Public
                        | BindingFlags.Instance
                );

                _botOwner = botOwner;
                _botInventoryController = (InventoryControllerClass)
                    botInventory.GetValue(botOwner.GetPlayer);
                _transactionController = new TransactionController(
                    _botOwner,
                    _botInventoryController,
                    _log
                );

                // Initialize current armor classs
                Item chest = _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.ArmorVest)
                    .ContainedItem;
                SearchableItemClass tacVest = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.TacticalVest)
                        .ContainedItem;
                ArmorComponent currentArmor = chest?.GetItemComponent<ArmorComponent>();
                ArmorComponent currentVest = tacVest?.GetItemComponent<ArmorComponent>();
                CurrentBodyArmorClass = currentArmor?.ArmorClass ?? currentVest?.ArmorClass ?? 0;

                CalculateGearValue();
            }
            catch (Exception e)
            {
                _log.LogError(e);
            }
        }

        public InventoryControllerClass GetInventoryController()
        {
            return _botInventoryController;
        }

        public void CalculateGearValue()
        {
            _log.LogDebug("Calculating gear value...");
            Item primary = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.FirstPrimaryWeapon)
                .ContainedItem;
            Item secondary = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.SecondPrimaryWeapon)
                .ContainedItem;
            Item holster = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.Holster)
                .ContainedItem;

            if (primary != null && GearValue.Primary.Id != primary.Id)
            {
                float value = LootingBots.ItemAppraiser.GetItemPrice(primary);
                GearValue.Primary = new ValuePair(primary.Id, value);
            }
            if (secondary != null && GearValue.Secondary.Id != secondary.Id)
            {
                float value = LootingBots.ItemAppraiser.GetItemPrice(secondary);
                GearValue.Secondary = new ValuePair(secondary.Id, value);
            }
            if (holster != null && GearValue.Holster.Id != holster.Id)
            {
                float value = LootingBots.ItemAppraiser.GetItemPrice(holster);
                GearValue.Holster = new ValuePair(holster.Id, value);
            }
        }

        public async Task<IResult> SortTacVest()
        {
            SearchableItemClass tacVest = (SearchableItemClass)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem;

            if (tacVest != null)
            {
                var result = LootUtils.SortContainer(tacVest, _botInventoryController);

                if (result.Succeeded)
                {
                    return await _transactionController.TryRunNetworkTransaction(result);
                }
            }
            return null;
        }

        /**
        * Main driving method which kicks off the logic for what a bot will do with the loot found on a corpse.
        * If bots are looting something that is equippable and they have nothing equipped in that slot, they will always equip it.
        * If the bot decides not to equip the item then it will attempt to put in an available container slot
        */
        public async Task<bool> TryAddItemsToBot(Item[] items)
        {
            foreach (Item item in items)
            {
                if (TransactionController.IsLootingInterrupted(_botOwner))
                {
                    return false;
                }

                if (item != null && item.Name != null)
                {
                    _log.LogDebug($"Loot found: {item.Name.Localized()}");
                    if (item is MagazineClass mag && !CanUseMag(mag))
                    {
                        _log.LogDebug($"Cannot use mag: {item.Name.Localized()}. Skipping");
                        continue;
                    }

                    // Check to see if we need to swap gear
                    TransactionController.EquipAction action = GetEquipAction(item);
                    if (action.Swap != null)
                    {
                        await _transactionController.ThrowAndEquip(action.Swap);
                        continue;
                    }
                    else if (action.Move != null)
                    {
                        await _transactionController.MoveItem(action.Move);
                        continue;
                    }

                    // Check to see if we can equip the item
                    bool ableToEquip = await _transactionController.TryEquipItem(item);

                    if (ableToEquip)
                    {
                        continue;
                    }

                    // Try to pick up any nested items before trying to pick up the item. This helps when looting rigs to transfer ammo to the bots active rig
                    bool success = await LootNestedItems(item);

                    if (!success)
                    {
                        return success;
                    }

                    // Check to see if we can pick up the item
                    bool ableToPickUp = await _transactionController.TryPickupItem(item);

                    if (ableToPickUp)
                    {
                        continue;
                    }
                }
                else
                {
                    _log.LogDebug("Item was null");
                }
            }

            return true;
        }

        /**
        * Checks certain slots to see if the item we are looting is "better" than what is currently equipped. View shouldSwapGear for criteria.
        * Gear is checked in a specific order so that bots will try to swap gear that is a "container" first like backpacks and tacVests to make sure
        * they arent putting loot in an item they will ultimately decide to drop
        */
        public TransactionController.EquipAction GetEquipAction(Item lootItem)
        {
            Item helmet = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.Headwear)
                .ContainedItem;
            Item chest = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.ArmorVest)
                .ContainedItem;
            Item tacVest = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.TacticalVest)
                .ContainedItem;
            Item backpack = _botInventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.Backpack)
                .ContainedItem;

            string lootID = lootItem?.Parent?.Container?.ID;
            TransactionController.EquipAction action = new TransactionController.EquipAction();
            TransactionController.SwapAction swapAction = null;

            if ((lootItem.Template is WeaponTemplate) && !((Weapon)lootItem).IsFlareGun)
            {
                return GetWeaponEquipAction(lootItem as Weapon);
            }

            if (backpack?.Parent?.Container.ID == lootID && ShouldSwapGear(backpack, lootItem))
            {
                swapAction = GetSwapAction(backpack, lootItem, null, true);
            }
            else if (helmet?.Parent?.Container?.ID == lootID && ShouldSwapGear(helmet, lootItem))
            {
                swapAction = GetSwapAction(helmet, lootItem);
            }
            else if (chest?.Parent?.Container?.ID == lootID && ShouldSwapGear(chest, lootItem))
            {
                swapAction = GetSwapAction(chest, lootItem);
            }
            else if (tacVest?.Parent?.Container?.ID == lootID && ShouldSwapGear(tacVest, lootItem))
            {
                // If the tac vest we are looting is higher armor class and we have a chest equipped, make sure to drop the chest and pick up the armored rig
                if (IsLootingBetterArmor(tacVest, lootItem) && chest != null)
                {
                    _log.LogDebug("Bot looting armored rig and dropping chest");
                    swapAction = GetSwapAction(
                        chest,
                        null,
                        async () =>
                            await _transactionController.ThrowAndEquip(
                                GetSwapAction(tacVest, lootItem, null, true)
                            )
                    );
                }
                else
                {
                    swapAction = GetSwapAction(tacVest, lootItem, null, true);
                }
            }

            action.Swap = swapAction;
            return action;
        }

        public bool CanUseMag(MagazineClass mag)
        {
            return _botInventoryController.Inventory.Equipment
                    .GetSlotsByName(
                        new EquipmentSlot[]
                        {
                            EquipmentSlot.FirstPrimaryWeapon,
                            EquipmentSlot.SecondPrimaryWeapon,
                            EquipmentSlot.Holster
                        }
                    )
                    .Where(
                        slot =>
                            slot.ContainedItem != null
                            && ((Weapon)slot.ContainedItem).GetMagazineSlot() != null
                            && ((Weapon)slot.ContainedItem).GetMagazineSlot().CanAccept(mag)
                    )
                    .ToArray()
                    .Length > 0;
        }

        public async Task ThrowUselessMags(Weapon thrownWeapon)
        {
            Weapon primary = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.FirstPrimaryWeapon)
                    .ContainedItem;
            Weapon secondary = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.SecondPrimaryWeapon)
                    .ContainedItem;
            Weapon holster = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Holster)
                    .ContainedItem;
            List<MagazineClass> mags = new List<MagazineClass>();
            _botInventoryController.GetReachableItemsOfTypeNonAlloc(mags);

            _log.LogDebug($"Bot {_botOwner.Id} cleaning up old mags...");
            int reservedCount = 0;
            foreach (MagazineClass mag in mags)
            {
                bool fitsInThrown =
                    thrownWeapon.GetMagazineSlot() != null
                    && thrownWeapon.GetMagazineSlot().CanAccept(mag);
                bool fitsInPrimary =
                    primary != null
                    && primary.GetMagazineSlot() != null
                    && primary.GetMagazineSlot().CanAccept(mag);
                bool fitsInSecondary =
                    secondary != null
                    && secondary.GetMagazineSlot() != null
                    && secondary.GetMagazineSlot().CanAccept(mag);
                bool fitsInHolster =
                    holster != null
                    && holster.GetMagazineSlot() != null
                    && holster.GetMagazineSlot().CanAccept(mag);

                bool fitsInEquipped = fitsInPrimary || fitsInSecondary || fitsInHolster;
                bool isSharedMag = fitsInThrown && fitsInEquipped;
                if (reservedCount < 2 && fitsInThrown && fitsInEquipped)
                {
                    _log.LogDebug(
                        $"Bot {_botOwner.Id} reserving shared mag {mag.Name.Localized()}"
                    );
                    reservedCount++;
                }
                else if ((reservedCount >= 2 && fitsInEquipped) || !fitsInEquipped)
                {
                    _log.LogDebug(
                        $"Bot {_botOwner.Id} removing useless mag {mag.Name.Localized()}"
                    );
                    await _transactionController.ThrowAndEquip(
                        new TransactionController.SwapAction(mag)
                    );
                }
            }

            usableMags.Remove(thrownWeapon.Id);
        }

        public TransactionController.EquipAction GetWeaponEquipAction(Weapon lootWeapon)
        {
            Weapon primary = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.FirstPrimaryWeapon)
                    .ContainedItem;
            Weapon secondary = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.SecondPrimaryWeapon)
                    .ContainedItem;
            Weapon holster = (Weapon)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Holster)
                    .ContainedItem;

            TransactionController.EquipAction action = new TransactionController.EquipAction();
            bool isPistol = lootWeapon.WeapClass.Equals("pistol");

            float lootValue = LootingBots.ItemAppraiser.GetItemPrice(lootWeapon);

            if (isPistol)
            {
                if (holster == null)
                {
                    action.Move = new TransactionController.MoveAction(
                        lootWeapon,
                        _botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    GearValue.Holster = new ValuePair(lootWeapon.Id, lootValue);
                }
                else if (holster != null && GearValue.Holster.Value < lootValue)
                {
                    _log.LogDebug(
                        $"Trying to swap {holster.Name.Localized()} (₽{GearValue.Holster.Value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                    );
                    action.Swap = GetSwapAction(holster, lootWeapon);
                    GearValue.Holster = new ValuePair(lootWeapon.Id, lootValue);
                }
            }
            else
            {
                // If we have no primary, just equip the weapon to primary
                if (primary == null)
                {
                    action.Move = new TransactionController.MoveAction(
                        lootWeapon,
                        _botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                }
                else if (GearValue.Primary.Value < lootValue)
                {
                    // If the loot weapon is worth more than the primary, by nature its also worth more than the secondary. Try to move the primary weapon to the secondary slot and equip the new weapon as the primary
                    if (secondary == null)
                    {
                        ItemAddress canEquipToSecondary = _botInventoryController.FindSlotToPickUp(
                            primary
                        );
                        _log.LogDebug(
                            $"Moving {primary.Name.Localized()} (₽{GearValue.Primary.Value}) to secondary and equipping {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );
                        action.Move = new TransactionController.MoveAction(
                            primary,
                            canEquipToSecondary,
                            null,
                            async () =>
                            {
                                // Delay to wait for animation to complete. Bot animation is playing for putting the primary weapon away
                                await Task.Delay(1000);
                                await _transactionController.TryEquipItem(lootWeapon);
                            }
                        );

                        GearValue.Secondary = GearValue.Primary;
                        GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                    // In the case where we have a secondary, throw it, move the primary to secondary, and equip the loot weapon as primary
                    else
                    {
                        _log.LogDebug(
                            $"Trying to swap {secondary.Name.Localized()} (₽{GearValue.Secondary.Value}) with {primary.Name.Localized()} (₽{GearValue.Primary.Value}) and equip {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );
                        action.Swap = GetSwapAction(
                            secondary,
                            primary,
                            null,
                            false,
                            async () =>
                            {
                                await Task.Delay(1000);
                                await ThrowUselessMags(secondary);
                                await _transactionController.TryEquipItem(lootWeapon);
                            }
                        );
                        GearValue.Secondary = GearValue.Primary;
                        GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                // If there is no secondary weapon, equip to secondary
                else if (secondary == null)
                {
                    action.Move = new TransactionController.MoveAction(
                        lootWeapon,
                        _botInventoryController.FindSlotToPickUp(lootWeapon)
                    );
                    GearValue.Secondary = new ValuePair(lootWeapon.Id, lootValue);
                }
                // If the loot weapon is worth more than the secondary, swap it
                else if (GearValue.Secondary.Value < lootValue)
                {
                    _log.LogDebug(
                        $"Trying to swap {secondary.Name.Localized()} (₽{GearValue.Secondary.Value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                    );
                    action.Swap = GetSwapAction(secondary, lootWeapon);
                    GearValue.Secondary = new ValuePair(secondary.Id, lootValue);
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
        public bool ShouldSwapGear(Item equipped, Item itemToLoot)
        {
            bool foundBiggerContainer = false;
            // If the item is a container, calculate the size and see if its bigger than what is equipped
            if (equipped.IsContainer)
            {
                int equippedSize = GetContainerSize(equipped as SearchableItemClass);
                int itemToLootSize = GetContainerSize(itemToLoot as SearchableItemClass);

                foundBiggerContainer = equippedSize < itemToLootSize;
            }

            bool foundBetterArmor = IsLootingBetterArmor(equipped, itemToLoot);
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
        public int GetContainerSize(SearchableItemClass container)
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
        public bool IsLootingBetterArmor(Item equipped, Item itemToLoot)
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
                foundBetterArmor = CurrentBodyArmorClass <= lootArmor.ArmorClass;

                if (foundBetterArmor)
                {
                    CurrentBodyArmorClass = lootArmor.ArmorClass;
                }
            }

            return foundBetterArmor;
        }

        /** Searches throught the child items of a container and attempts to loot them */
        public async Task<bool> LootNestedItems(Item parentItem)
        {
            if (TransactionController.IsLootingInterrupted(_botOwner))
            {
                return false;
            }

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
                            && !IsSingleUseKey(nestedItem)
                    )
                    .ToArray();

                if (containerItems.Length > 0)
                {
                    _log.LogDebug(
                        $"Looting {containerItems.Length} items from {parentItem.Name.Localized()}"
                    );
                    await TransactionController.SimulatePlayerDelay(1000);
                    return await TryAddItemsToBot(containerItems);
                }
            }
            else
            {
                _log.LogDebug($"No nested items found in {parentItem.Name}");
            }

            return true;
        }

        // Prevents bots from looting single use quest keys like "Unknown Key"
        public bool IsSingleUseKey(Item item)
        {
            KeyComponent key = item.GetItemComponent<KeyComponent>();
            return key != null && key.Template.MaximumNumberOfUsage == 1;
        }

        /** Generates a SwapAction to send to the transaction controller*/
        public TransactionController.SwapAction GetSwapAction(
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
                    await TransactionController.SimulatePlayerDelay();
                    await LootNestedItems(toThrow);
                };
            }

            return new TransactionController.SwapAction(
                toThrow,
                toEquip,
                callback
                    ?? (
                        async () =>
                        {
                            LootCache.AddVisitedLoot(_botOwner.Id, toThrow.Id);
                            await TransactionController.SimulatePlayerDelay(1000);

                            if (toThrow is Weapon weapon)
                            {
                                await ThrowUselessMags(weapon);
                            }
                            // Try to equip the item after throwing
                            await _transactionController.TryEquipItem(toEquip);
                        }
                    ),
                onComplete ?? onSwapComplete
            );
        }
    }
}
