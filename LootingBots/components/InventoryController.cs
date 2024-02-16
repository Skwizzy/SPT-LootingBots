using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Comfort.Common;

using EFT;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

using UnityEngine;

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

    public class BotStats
    {
        public float NetLootValue;
        public int AvailableGridSpaces;
        public int TotalGridSpaces;

        public GearValue WeaponValues = new GearValue();

        public void AddNetValue(float itemPrice)
        {
            NetLootValue += itemPrice;
        }

        public void SubtractNetValue(float itemPrice)
        {
            NetLootValue += itemPrice;
        }

        public void StatsDebugPanel(StringBuilder debugPanel)
        {
            Color freeSpaceColor =
                AvailableGridSpaces <= 2
                    ? Color.red
                    : AvailableGridSpaces < TotalGridSpaces / 2
                        ? Color.yellow
                        : Color.green;

            debugPanel.AppendLabeledValue(
                $"Total looted value",
                $" {NetLootValue:n0}₽",
                Color.white,
                Color.white
            );
            debugPanel.AppendLabeledValue(
                $"Available space",
                $" {AvailableGridSpaces} slots",
                Color.white,
                freeSpaceColor
            );
        }
    }

    public class InventoryController
    {
        private readonly BotLog _log;
        private readonly TransactionController _transactionController;
        private readonly BotOwner _botOwner;
        private readonly InventoryControllerClass _botInventoryController;
        private readonly LootingBrain _lootingBrain;
        private readonly ItemAppraiser _itemAppraiser;

        public BotStats Stats = new BotStats();

        private static readonly GearValue GearValue = new GearValue();

        // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
        public int CurrentBodyArmorClass = 0;

        // Represents the value in roubles of the current item
        public float CurrentItemPrice = 0f;

        public bool ShouldSort = true;

        public InventoryController(BotOwner botOwner, LootingBrain lootingBrain)
        {
            try
            {
                _log = new BotLog(LootingBots.LootLog, botOwner);
                _lootingBrain = lootingBrain;
                _itemAppraiser = LootingBots.ItemAppraiser;

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
                UpdateGridStats();
            }
            catch (Exception e)
            {
                if (_log.ErrorEnabled)
                    _log.LogError(e);
            }
        }

        /**
        * Disable the tranaction controller to ensure transactions do not occur when the looting layer is interrupted
        */
        public void DisableTransactions()
        {
            _transactionController.Enabled = false;
        }

        /**
        * Used to enable the transaction controller when the looting layer is active
        */
        public void EnableTransactions()
        {
            _transactionController.Enabled = true;
        }

        /**
        * Calculates the value of the bot's current weapons to use in weapon swap comparison checks
        */
        public void CalculateGearValue()
        {
            if (_log.DebugEnabled)
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
                float value = _itemAppraiser.GetItemPrice(primary);
                GearValue.Primary = new ValuePair(primary.Id, value);
            }
            if (secondary != null && GearValue.Secondary.Id != secondary.Id)
            {
                float value = _itemAppraiser.GetItemPrice(secondary);
                GearValue.Secondary = new ValuePair(secondary.Id, value);
            }
            if (holster != null && GearValue.Holster.Id != holster.Id)
            {
                float value = _itemAppraiser.GetItemPrice(holster);
                GearValue.Holster = new ValuePair(holster.Id, value);
            }
        }

        /**
        * Updates stats for AvailableGridSpaces and TotalGridSpaces based off the bots current gear
        */
        public void UpdateGridStats()
        {
            SearchableItemClass tacVest = (SearchableItemClass)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem;
            SearchableItemClass backpack = (SearchableItemClass)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Backpack)
                    .ContainedItem;
            SearchableItemClass pockets = (SearchableItemClass)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Pockets)
                    .ContainedItem;

            int freePockets = LootUtils.GetAvailableGridSlots(pockets?.Grids);
            int freeTacVest = LootUtils.GetAvailableGridSlots(tacVest?.Grids);
            int freeBackpack = LootUtils.GetAvailableGridSlots(backpack?.Grids);

            Stats.AvailableGridSpaces = freeBackpack + freePockets + freeTacVest;
            Stats.TotalGridSpaces =
                (tacVest?.Grids?.Length ?? 0)
                + (backpack?.Grids?.Length ?? 0)
                + (pockets?.Grids?.Length ?? 0);
        }

        /**
        * Sorts the items in the tactical vest so that items prefer to be in slots that match their size. I.E a 1x1 item will be placed in a 1x1 slot instead of a 1x2 slot
        */
        public async Task<IResult> SortTacVest()
        {
            SearchableItemClass tacVest = (SearchableItemClass)
                _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem;

            ShouldSort = false;

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
        * Main driving method which kicks off the logic for what a bot will do with the loot found.
        * If bots are looting something that is equippable and they have nothing equipped in that slot, they will always equip it.
        * If the bot decides not to equip the item then it will attempt to put in an available container slot
        */
        public async Task<bool> TryAddItemsToBot(Item[] items)
        {
            foreach (Item item in items)
            {
                if (item != null && item.Name != null)
                {
                    if (LootingBots.UseExamineTime.Value)
                    {
                        await SimulateExamineTime(item);
                    }

                    if (_transactionController.IsLootingInterrupted())
                    {
                        UpdateKnownItems();
                        return false;
                    }

                    CurrentItemPrice = _itemAppraiser.GetItemPrice(item);

                    if (_log.InfoEnabled)
                        _log.LogInfo($"Loot found: {item.Name.Localized()} ({CurrentItemPrice}₽)");

                    // Ignore magazines that a bot cannot actively use
                    if (item is MagazineClass mag && !IsUsableMag(mag))
                    {
                        if (_log.DebugEnabled)
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
                        if (_log.DebugEnabled)
                            _log.LogDebug("Moving due to GetEquipAction");

                        if (await _transactionController.MoveItem(action.Move))
                        {
                            Stats.AddNetValue(CurrentItemPrice);
                        }
                        continue;
                    }

                    // Check to see if we can equip the item
                    if (AllowedToEquip(item) && await _transactionController.TryEquipItem(item))
                    {
                        Stats.AddNetValue(CurrentItemPrice);
                        continue;
                    }

                    // If the item we are trying to pickup is a weapon, we need to perform the "pickup" action before trying to strip the weapon of its mods. This is to
                    // prevent stripping the mods from a weapon and then picking up the weapon afterwards.
                    if (item is Weapon weapon)
                    {
                        if (
                            AllowedToPickup(weapon)
                            && await _transactionController.TryPickupItem(weapon)
                        )
                        {
                            Stats.AddNetValue(CurrentItemPrice);
                            UpdateGridStats();
                            continue;
                        }

                        if (LootingBots.CanStripAttachments.Value)
                        {
                            // Strip the weapon of its mods if we cannot pickup the weapon
                            bool success = await TryAddItemsToBot(
                                weapon.Mods.Where(mod => !mod.IsUnremovable).ToArray()
                            );

                            if (!success)
                            {
                                UpdateKnownItems();
                                return success;
                            }
                        }
                    }

                    // Try to pick up any nested items before trying to pick up the item. This helps when looting rigs to transfer ammo to the bots active rig
                    if (item is SearchableItemClass)
                    {
                        bool success = await LootNestedItems(item);

                        if (!success)
                        {
                            UpdateKnownItems();
                            return success;
                        }
                    }

                    // Check to see if we can pick up the item
                    if (AllowedToPickup(item) && await _transactionController.TryPickupItem(item))
                    {
                        Stats.AddNetValue(CurrentItemPrice);
                        UpdateGridStats();
                        continue;
                    }
                }
                else if (_log.DebugEnabled)
                {
                    _log.LogDebug("Item was null");
                }
            }

            // Refresh bot's known items dictionary
            UpdateKnownItems();

            return true;
        }

        /** Use the ExamineTime of an object and the AttentionExamineValue of the bot to calculate the delay for discovering an item while looting */
        public Task SimulateExamineTime(Item item)
        {
            // Taken from GClass2665 constructor
            return TransactionController.SimulatePlayerDelay(
                item.ExamineTime * 1000f / (1f + _botOwner.Profile.Skills.AttentionExamineValue)
            );
        }

        /**
        * Method to make the bot change to its primary weapon. Useful for making sure bots have their weapon out after they have swapped weapons.
        */
        public void ChangeToPrimary()
        {
            if (_botOwner != null && _botOwner.WeaponManager?.Selector != null)
            {
                if (_log.InfoEnabled)
                    _log.LogInfo($"Changing to primary");

                _botOwner.WeaponManager.UpdateWeaponsList();
                _botOwner.WeaponManager.Selector.ChangeToMain();
                RefillAndReload();
            }
        }

        /**
        * Updates the bot's known weapon list and tells the bot to switch to its main weapon
        */
        public void UpdateActiveWeapon()
        {
            if (_botOwner != null && _botOwner.WeaponManager?.Selector != null)
            {
                if (_log.InfoEnabled)
                    _log.LogInfo($"Updating weapons");

                _botOwner.WeaponManager.UpdateWeaponsList();
                _botOwner.WeaponManager.Selector.TakeMainWeapon();
                RefillAndReload();
            }
        }

        /**
        * Method to refill magazines with ammo and also reload the current weapon with a new magazine
        */
        private void RefillAndReload()
        {
            if (_botOwner != null && _botOwner.WeaponManager?.Selector != null)
            {
                _botOwner.WeaponManager.Reload.TryFillMagazines();
                _botOwner.WeaponManager.Reload.TryReload();
            }
        }

        /** Marks all items placed in rig/pockets/backpack as known items that they are able to use */
        public void UpdateKnownItems()
        {
            // Protection against bot death interruption
            if (_botOwner != null && _botInventoryController != null)
            {
                SearchableItemClass tacVest = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.TacticalVest)
                        .ContainedItem;
                SearchableItemClass backpack = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.Backpack)
                        .ContainedItem;
                SearchableItemClass pockets = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.Pockets)
                        .ContainedItem;
                SearchableItemClass secureContainer = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.SecuredContainer)
                        .ContainedItem;

                tacVest?.UncoverAll(_botOwner.ProfileId);
                backpack?.UncoverAll(_botOwner.ProfileId);
                pockets?.UncoverAll(_botOwner.ProfileId);
                secureContainer?.UncoverAll(_botOwner.ProfileId);
            }
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

            if (!AllowedToEquip(lootItem))
            {
                return action;
            }

            if (
                lootItem.Template is WeaponTemplate
                && !BotTypeUtils.IsBoss(_botOwner.Profile.Info.Settings.Role)
            )
            {
                return GetWeaponEquipAction(lootItem as Weapon);
            }

            if (EquipmentTypeUtils.IsBackpack(lootItem) && ShouldSwapGear(backpack, lootItem))
            {
                swapAction = GetSwapAction(backpack, lootItem, null, true);
            }
            else if (EquipmentTypeUtils.IsHelmet(lootItem) && ShouldSwapGear(helmet, lootItem))
            {
                swapAction = GetSwapAction(helmet, lootItem);
            }
            else if (EquipmentTypeUtils.IsArmorVest(lootItem) && ShouldSwapGear(chest, lootItem))
            {
                swapAction = GetSwapAction(chest, lootItem);
            }
            else if (
                EquipmentTypeUtils.IsTacticalRig(lootItem) && ShouldSwapGear(tacVest, lootItem)
            )
            {
                // If the tac vest we are looting is higher armor class and we have a chest equipped, make sure to drop the chest and pick up the armored rig
                if (IsLootingBetterArmor(tacVest, lootItem) && chest != null)
                {
                    if (_log.DebugEnabled)
                        _log.LogDebug("Looting armored rig and dropping chest");

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

        public bool IsUsableMag(MagazineClass mag)
        {
            return mag != null
                && _botInventoryController.Inventory.Equipment
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

        /**
        * Throws all magazines from the rig that are not able to be used by any of the weapons that the bot currently has equipped
        */
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

            if (_log.DebugEnabled)
                _log.LogDebug($"Cleaning up old mags...");

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
                    if (_log.DebugEnabled)
                        _log.LogDebug($"Reserving shared mag {mag.Name.Localized()}");

                    reservedCount++;
                }
                else if ((reservedCount >= 2 && fitsInEquipped) || !fitsInEquipped)
                {
                    if (_log.DebugEnabled)
                        _log.LogDebug($"Removing useless mag {mag.Name.Localized()}");

                    await _transactionController.ThrowAndEquip(
                        new TransactionController.SwapAction(mag)
                    );
                }
            }
        }

        /**
        * Determines the kind of equip action the bot should take when encountering a weapon. Bots will always prefer to replace weapons that have lower value when encountering a higher value weapon.
        */
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
            float lootValue = CurrentItemPrice;

            if (isPistol)
            {
                if (holster == null)
                {
                    var place = _botInventoryController.FindSlotToPickUp(lootWeapon);
                    if (place != null)
                    {
                        action.Move = new TransactionController.MoveAction(lootWeapon, place);
                        GearValue.Holster = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                else if (holster != null && GearValue.Holster.Value < lootValue)
                {
                    if (_log.DebugEnabled)
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
                    var place = _botInventoryController.FindSlotToPickUp(lootWeapon);
                    if (place != null)
                    {
                        action.Move = new TransactionController.MoveAction(
                            lootWeapon,
                            place,
                            null,
                            async () =>
                            {
                                ChangeToPrimary();
                                Stats.AddNetValue(lootValue);
                                await TransactionController.SimulatePlayerDelay(1000);
                            }
                        );
                        GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                else if (GearValue.Primary.Value < lootValue)
                {
                    // If the loot weapon is worth more than the primary, by nature its also worth more than the secondary. Try to move the primary weapon to the secondary slot and equip the new weapon as the primary
                    if (secondary == null)
                    {
                        ItemAddress place = _botInventoryController.FindSlotToPickUp(primary);
                        if (place != null)
                        {
                            if (_log.DebugEnabled)
                                _log.LogDebug(
                                    $"Moving {primary.Name.Localized()} (₽{GearValue.Primary.Value}) to secondary and equipping {lootWeapon.Name.Localized()} (₽{lootValue})"
                                );

                            action.Move = new TransactionController.MoveAction(
                                primary,
                                place,
                                null,
                                async () =>
                                {
                                    await _transactionController.TryEquipItem(lootWeapon);
                                    await TransactionController.SimulatePlayerDelay(1500);
                                    ChangeToPrimary();
                                }
                            );

                            GearValue.Secondary = GearValue.Primary;
                            GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                        }
                    }
                    // In the case where we have a secondary, throw it, move the primary to secondary, and equip the loot weapon as primary
                    else
                    {
                        if (_log.DebugEnabled)
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
                                await ThrowUselessMags(secondary);
                                await _transactionController.TryEquipItem(lootWeapon);
                                Stats.AddNetValue(lootValue);
                                await TransactionController.SimulatePlayerDelay(1500);
                                ChangeToPrimary();
                            }
                        );
                        GearValue.Secondary = GearValue.Primary;
                        GearValue.Primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                // If there is no secondary weapon, equip to secondary
                else if (secondary == null)
                {
                    var place = _botInventoryController.FindSlotToPickUp(lootWeapon);
                    if (place != null)
                    {
                        action.Move = new TransactionController.MoveAction(
                            lootWeapon,
                            _botInventoryController.FindSlotToPickUp(lootWeapon)
                        );
                        GearValue.Secondary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                // If the loot weapon is worth more than the secondary, swap it
                else if (GearValue.Secondary.Value < lootValue)
                {
                    if (_log.DebugEnabled)
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
            if (equipped == null)
            {
                return false;
            }

            // Bosses cannot swap gear as many bosses have custom logic tailored to their loadouts
            if (BotTypeUtils.IsBoss(_botOwner.Profile.Info.Settings.Role))
            {
                return false;
            }

            bool foundBiggerContainer = false;
            // If the item is a container, calculate the size and see if its bigger than what is equipped
            if (equipped.IsContainer)
            {
                int equippedSize = LootUtils.GetContainerSize(equipped as SearchableItemClass);
                int itemToLootSize = LootUtils.GetContainerSize(itemToLoot as SearchableItemClass);

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
            if (_transactionController.IsLootingInterrupted())
            {
                return false;
            }

            Item[] items = parentItem
                .GetFirstLevelItems()
                .ToArray()
                .Where(
                    // Filter out the parent item from the list, quest items, and single use keys
                    nestedItem =>
                        nestedItem.Id != parentItem.Id
                        && !nestedItem.QuestItem
                        && !LootUtils.IsSingleUseKey(nestedItem)
                )
                .ToArray();

            if (items.Length > 0)
            {
                if (_log.DebugEnabled)
                    _log.LogDebug(
                        $"Looting {items.Length} items from {parentItem.Name.Localized()}"
                    );

                await TransactionController.SimulatePlayerDelay(LootingBrain.LootingStartDelay);
                return await TryAddItemsToBot(items);
            }
            else if (_log.DebugEnabled)
            {
                _log.LogDebug($"No nested items found in {parentItem.Name}");
            }

            return true;
        }

        /**
            Check if the item being looted meets the loot value threshold specified in the mod settings and saves its value in CurrentItemPrice.
            PMC bots use the PMC loot threshold, all other bots such as scavs, bosses, and raiders will use the scav threshold
        */
        public bool IsValuableEnough(float itemPrice)
        {
            WildSpawnType botType = _botOwner.Profile.Info.Settings.Role;
            bool isPMC = BotTypeUtils.IsPMC(botType);

            // If the bot is a PMC, compare the price against the PMC loot threshold. For all other bot types use the scav threshold
            float min = (
                isPMC ? LootingBots.PMCMinLootThreshold : LootingBots.ScavMinLootThreshold
            ).Value;
            float max = (
                isPMC ? LootingBots.PMCMaxLootThreshold : LootingBots.ScavMaxLootThreshold
            ).Value;

            // If max is set to 0, do not check agains max threshold
            return itemPrice >= min && (max == 0f || itemPrice <= max);
        }

        public bool AllowedToEquip(Item lootItem)
        {
            WildSpawnType botType = _botOwner.Profile.Info.Settings.Role;
            bool isPMC = BotTypeUtils.IsPMC(botType);
            bool allowedToEquip = isPMC
                ? LootingBots.PMCGearToEquip.Value.IsItemEligible(lootItem)
                : LootingBots.ScavGearToEquip.Value.IsItemEligible(lootItem);

            return allowedToEquip;
        }

        public bool AllowedToPickup(Item lootItem)
        {
            WildSpawnType botType = _botOwner.Profile.Info.Settings.Role;
            bool isPMC = BotTypeUtils.IsPMC(botType);
            bool pickupNotRestricted = isPMC
                ? LootingBots.PMCGearToPickup.Value.IsItemEligible(lootItem)
                : LootingBots.ScavGearToPickup.Value.IsItemEligible(lootItem);
            bool isMoney = lootItem.Template is MoneyClass;

            // All usable mags and money should be considered eligible to loot. Otherwise all other items fall subject to the mod settings for restricting pickup and loot value thresholds
            return IsUsableMag(lootItem as MagazineClass)
                || isMoney
                || (pickupNotRestricted && IsValuableEnough(CurrentItemPrice));
        }

        /**
        *   Returns the list of slots to loot from a corpse in priority order. When a bot already has a backpack/rig, they will attempt to loot the weapons off the bot first. Otherwise they will loot the equipement first and loot the weapons afterwards.
        */
        public EquipmentSlot[] GetPrioritySlots()
        {
            InventoryControllerClass botInventoryController = _botInventoryController;
            bool hasBackpack =
                botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Backpack)
                    .ContainedItem != null;
            bool hasTacVest =
                botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.TacticalVest)
                    .ContainedItem != null;

            EquipmentSlot[] prioritySlots = new EquipmentSlot[0];
            EquipmentSlot[] weaponSlots = new EquipmentSlot[]
            {
                EquipmentSlot.Holster,
                EquipmentSlot.FirstPrimaryWeapon,
                EquipmentSlot.SecondPrimaryWeapon
            };
            EquipmentSlot[] storageSlots = new EquipmentSlot[]
            {
                EquipmentSlot.Backpack,
                EquipmentSlot.ArmorVest,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Pockets
            };

            if (hasBackpack || hasTacVest)
            {
                if (_log.DebugEnabled)
                    _log.LogDebug($"Has backpack/rig and is looting weapons first!");

                prioritySlots = prioritySlots.Concat(weaponSlots).Concat(storageSlots).ToArray();
            }
            else
            {
                prioritySlots = prioritySlots.Concat(storageSlots).Concat(weaponSlots).ToArray();
            }

            return prioritySlots
                .Concat(
                    new EquipmentSlot[]
                    {
                        EquipmentSlot.Headwear,
                        EquipmentSlot.Earpiece,
                        EquipmentSlot.Dogtag,
                        EquipmentSlot.Scabbard,
                        EquipmentSlot.FaceCover
                    }
                )
                .ToArray();
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
                            Stats.SubtractNetValue(_itemAppraiser.GetItemPrice(toThrow));
                            _lootingBrain.IgnoreLoot(toThrow.Id);
                            await TransactionController.SimulatePlayerDelay(1000);

                            if (toThrow is Weapon weapon)
                            {
                                await ThrowUselessMags(weapon);
                            }

                            bool isMovingOwnedItem = _botInventoryController.IsItemEquipped(
                                toEquip
                            );
                            // Try to equip the item after throwing
                            if (
                                await _transactionController.TryEquipItem(toEquip)
                                && !isMovingOwnedItem
                            )
                            {
                                Stats.AddNetValue(CurrentItemPrice);
                            }
                        }
                    ),
                onComplete ?? onSwapComplete
            );
        }
    }
}
