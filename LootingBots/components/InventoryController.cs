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
            debugPanel.AppendLabeledValue(
                $"Primary Value",
                $" {WeaponValues.Primary.Value:n0}₽",
                Color.white,
                Color.white
            );
            debugPanel.AppendLabeledValue(
                $"Secondary Value",
                $" {WeaponValues.Secondary.Value:n0}₽",
                Color.white,
                Color.white
            );
            debugPanel.AppendLabeledValue(
                $"Holster Value",
                $" {WeaponValues.Holster.Value:n0}₽",
                Color.white,
                Color.white
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

        public ArmorComponent CurrentArmorVest
        {
            get
            {
                Item chest = _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.ArmorVest)
                    .ContainedItem;
                return chest?.GetItemComponent<ArmorComponent>();
            }
        }

        public ArmorComponent CurrentArmorRig
        {
            get
            {
                SearchableItemClass tacVest = (SearchableItemClass)
                    _botInventoryController.Inventory.Equipment
                        .GetSlot(EquipmentSlot.TacticalVest)
                        .ContainedItem;
                return tacVest?.GetItemComponent<ArmorComponent>();
            }
        }

        public ArmorComponent CurrentHeadArmor
        {
            get
            {
                Item helmet = _botInventoryController.Inventory.Equipment
                    .GetSlot(EquipmentSlot.Headwear)
                    .ContainedItem;
                return helmet?.GetItemComponent<ArmorComponent>();
            }
        }

        public ArmorComponent CurrentTorsoArmor
        {
            get { return CurrentArmorRig ?? CurrentArmorVest; }
        }

        public int CurrentTorsoArmorClass
        {
            get { return CurrentTorsoArmor?.ArmorClass ?? 0; }
        }

        public int CurrentHeadArmorClass
        {
            get { return CurrentHeadArmor?.ArmorClass ?? 0; }
        }

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
                _botInventoryController = LootUtils.GetBotInventoryController(botOwner.GetPlayer);
                _botOwner = botOwner;
                _transactionController = new TransactionController(
                    _botOwner,
                    _botInventoryController,
                    _log
                );

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

            if (primary != null && Stats.WeaponValues.Primary.Id != primary.Id)
            {
                float value = _itemAppraiser.GetItemPrice(primary);
                Stats.WeaponValues.Primary = new ValuePair(primary.Id, value);
            }
            if (secondary != null && Stats.WeaponValues.Secondary.Id != secondary.Id)
            {
                float value = _itemAppraiser.GetItemPrice(secondary);
                Stats.WeaponValues.Secondary = new ValuePair(secondary.Id, value);
            }
            if (holster != null && Stats.WeaponValues.Holster.Id != holster.Id)
            {
                float value = _itemAppraiser.GetItemPrice(holster);
                Stats.WeaponValues.Holster = new ValuePair(holster.Id, value);
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
        public async Task<bool> TryAddItemsToBot(IEnumerable<Item> items)
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
                        if (_log.WarningEnabled)
                            _log.LogWarning(
                                $"Moving {action.Move.ToMove.Name.Localized()} to: {action.Move.Place.Container.ID.Localized()}"
                            );

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
                    else if (item is Weapon weapon && LootingBots.CanStripAttachments.Value)
                    {
                        // Strip the weapon of its mods if we cannot pickup the weapon
                        bool success = await TryAddItemsToBot(
                            weapon.Slots
                                .Where(slot => !slot.Required)
                                .SelectMany(slot => slot.Items.Where(modItem => modItem is Mod mod && mod.RaidModdable))
                        );

                        if (!success)
                        {
                            UpdateKnownItems();
                            return success;
                        }
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
                if (GetArmorDifference(tacVest, lootItem) > 0 && chest != null)
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
                        Stats.WeaponValues.Holster = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                else if (Stats.WeaponValues.Holster.Value < lootValue)
                {
                    if (_log.DebugEnabled)
                        _log.LogDebug(
                            $"Trying to swap {holster.Name.Localized()} (₽{Stats.WeaponValues.Holster.Value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );

                    action.Swap = GetSwapAction(holster, lootWeapon);
                    Stats.WeaponValues.Holster = new ValuePair(lootWeapon.Id, lootValue);
                }
            }
            else
            {
                bool isBetterThanPrimary = Stats.WeaponValues.Primary.Value < lootValue;
                bool isBetterThanSecondary = Stats.WeaponValues.Secondary.Value < lootValue;

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
                        Stats.WeaponValues.Primary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                else if (isBetterThanPrimary)
                {
                    // If the weapon is better than the primary and there is no secondary, move the primary to secondary and equip the new weapon as the primary
                    if (secondary == null)
                    {
                        ItemAddress place = _botInventoryController.FindSlotToPickUp(primary);
                        if (place != null)
                        {
                            if (_log.DebugEnabled)
                                _log.LogDebug(
                                    $"Moving {primary.Name.Localized()} (₽{Stats.WeaponValues.Primary.Value}) to secondary and equipping {lootWeapon.Name.Localized()} (₽{lootValue})"
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

                            Stats.WeaponValues.Secondary = Stats.WeaponValues.Primary;
                            Stats.WeaponValues.Primary = new ValuePair(lootWeapon.Id, lootValue);
                        }
                    }
                    // If the weapon is also better than the secondary, throw the secondary and move the primary to secondary before equipping the new weapon to primary
                    else if (isBetterThanSecondary)
                    {
                        if (_log.DebugEnabled)
                            _log.LogDebug(
                                $"Trying to swap {secondary.Name.Localized()} (₽{Stats.WeaponValues.Secondary.Value}) with {primary.Name.Localized()} (₽{Stats.WeaponValues.Primary.Value}) and equip {lootWeapon.Name.Localized()} (₽{lootValue})"
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
                        Stats.WeaponValues.Secondary = Stats.WeaponValues.Primary;
                        Stats.WeaponValues.Primary = new ValuePair(lootWeapon.Id, lootValue);
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
                        Stats.WeaponValues.Secondary = new ValuePair(lootWeapon.Id, lootValue);
                    }
                }
                // If the loot weapon is worth more than the secondary, swap it
                else if (isBetterThanSecondary)
                {
                    if (_log.DebugEnabled)
                        _log.LogDebug(
                            $"Trying to swap {secondary.Name.Localized()} (₽{Stats.WeaponValues.Secondary.Value}) with {lootWeapon.Name.Localized()} (₽{lootValue})"
                        );

                    action.Swap = GetSwapAction(secondary, lootWeapon);
                    Stats.WeaponValues.Secondary = new ValuePair(secondary.Id, lootValue);
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

            int armorDifference = GetArmorDifference(equipped, itemToLoot);

            bool foundBetterArmor = armorDifference > 0; // Equip if we found item with a better armor class.
            bool pickupBiggerContainer = armorDifference == 0 && foundBiggerContainer; // If the item is bigger than what is equipped, only equip it if the armor class is the same

            return foundBetterArmor || pickupBiggerContainer;
        }

        /** Given a piece of armor, compare it against what is curren */
        public bool IsBetterArmorThanEquipped(ArmorClass newArmor)
        {
            ArmorComponent equippedArmor = EquipmentTypeUtils.IsHelmet(newArmor)
                ? CurrentHeadArmor
                : CurrentTorsoArmor;
            return GetArmorDifference(equippedArmor?.Item, newArmor) > 0;
        }

        /**
        * Returns an integer representing the difference between the armor classes of the itemToLoot and the currently equippedItem
        */
        public int GetArmorDifference(Item equippedItem, Item itemToLoot)
        {
            ArmorComponent newArmor = itemToLoot.GetItemComponent<ArmorComponent>();
            ArmorComponent currentArmor = equippedItem?.GetItemComponent<ArmorComponent>();

            int currentArmorClass = currentArmor?.ArmorClass ?? 0;
            int newArmorClass = newArmor?.ArmorClass ?? 0;

            return newArmorClass - currentArmorClass;
        }

        /** Searches throught the child items of a container and attempts to loot them */
        public async Task<bool> LootNestedItems(Item parentItem)
        {
            if (_transactionController.IsLootingInterrupted())
            {
                return false;
            }

            // If the parentItem is an item that has slots such as armor, find any slots that are locked and return the list of items in those slots to use later
            IEnumerable<Item> lockedItems = parentItem is LootItemClass itemWithSlots
                ? LootUtils.GetAllLockedItems(itemWithSlots)
                : null;

            IEnumerable<Item> items = parentItem
                .GetFirstLevelItems()
                .Where(
                // Filter out the parent item from the list, quest items, and single use keys
                nestedItem =>
                {
                    bool isItemLocked = lockedItems != null && lockedItems.Contains(nestedItem);

                    return nestedItem.Id != parentItem.Id
                        && !nestedItem.QuestItem
                        && !isItemLocked
                        && !LootUtils.IsSingleUseKey(nestedItem);
                });

            if (items.Count() > 0)
            {
                if (_log.DebugEnabled)
                    _log.LogDebug(
                        $"Looting {items.Count()} items from {parentItem.Name.Localized()}"
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
