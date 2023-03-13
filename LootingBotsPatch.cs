using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
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
            return typeof(GClass452).GetMethod("PickupAction", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool PatchPrefix(Player owner, GInterface264 possibleAction, Item rootItem, Player lootItemLastOwner)
        {
            Logger.LogDebug($"Someone is picking up: {rootItem.Name.Localized()}");
            return true;
        }
    }

    public class LootCorpsePatch : ModulePatch
    {
        private static MethodInfo _method_7;

        protected override MethodBase GetTargetMethod()
        {
            _method_7 = typeof(GClass325).GetMethod("method_7", BindingFlags.NonPublic | BindingFlags.Instance);

            return typeof(GClass325).GetMethod("method_6", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref GClass325 __instance, ref BotOwner ___botOwner_0, ref GClass263 ___gclass263_0)
        {
            Logger.LogDebug($"In corpse method");
            ItemAdder itemAdder = new ItemAdder(___botOwner_0);

            try
            {
                Player corpse = ___gclass263_0.Player;
                Type corpseType = corpse.GetType();
                FieldInfo corpseInventory = corpseType.BaseType.GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance);
                InventoryControllerClass corpseInventoryController = (InventoryControllerClass)corpseInventory.GetValue(corpse);

                Item[] priorityLoot = new Item[3] { null, null, null };
                Item[] priorityItems = corpseInventoryController.ContainedItems.Where(item => item?.Parent?.Container?.ID?.ToLower() == "backpack" || item?.Parent?.Container?.ID?.ToLower() == "tacticalvest" || item?.Parent?.Container?.ID?.ToLower() == "armorvest").ToArray();

                foreach (Item proritizedItem in priorityItems)
                {
                    bool isBackpack = proritizedItem.Parent.Container.ID.ToLower() == "backpack";
                    bool isChest = proritizedItem.Parent.Container.ID.ToLower() == "armorvest";
                    if (isBackpack)
                    {
                        priorityLoot[0] = proritizedItem;
                    }
                    else if (isChest)
                    {
                        priorityLoot[1] = proritizedItem;
                    }
                    else
                    {
                        priorityLoot[2] = proritizedItem;
                    }
                }

                itemAdder.tryAddItemsToBot(priorityLoot);

                // TODO: Filter out items marked as "untakable"
                Item[] containedItems = corpseInventoryController.ContainedItems.Where(item => item?.Parent?.Container?.ID?.ToLower() != "backpack" && item?.Parent?.Container?.ID?.ToLower() != "tacticalvest" && item?.Parent?.Container?.ID?.ToLower() != "armorvest").ToArray();
                itemAdder.tryAddItemsToBot(containedItems);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }

        // TODO: When picking up guns, see if you can get them to switch weapons after equipping
        public class ItemAdder
        {
            public BotOwner botOwner_0;
            public InventoryControllerClass botInventoryController;

            public int currentBodyArmorClass = 0;

            public ItemAdder(BotOwner botOwner_0)
            {
                try
                {
                    Type botOwnerType = botOwner_0.GetPlayer.GetType();
                    FieldInfo botInventory = botOwnerType.BaseType.GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance);

                    this.botOwner_0 = botOwner_0;
                    this.botInventoryController = (InventoryControllerClass)botInventory.GetValue(botOwner_0.GetPlayer);

                    Item chest = this.botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem;
                    Item tacVest = this.botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem;
                    ArmorComponent currentArmor = chest?.GetItemComponent<ArmorComponent>();
                    ArmorComponent currentVest = tacVest?.GetItemComponent<ArmorComponent>();

                    if (currentArmor != null)
                    {
                        this.currentBodyArmorClass = currentArmor.ArmorClass;
                    }
                    else if (currentVest != null)
                    {
                        this.currentBodyArmorClass = currentVest.ArmorClass;
                    }

                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }

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

            public bool considerArmorClass(Item equipped, Item itemToLoot)
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
                    // Logger.LogDebug($"Loot has armor class: {lootArmor.ArmorClass}");
                    // Logger.LogDebug($"Loot has durability: {lootArmor.Repairable.Durability}");
                    // Logger.LogDebug($"Loot has max durability: {lootArmor.Repairable.MaxDurability}");
                }
                else if (lootArmor != null)
                {
                    // If we are looting chest/rig with armor, check to see if it has a better armor class than what is equipped
                    foundBetterArmor = currentBodyArmorClass <= lootArmor.ArmorClass;
                    // Logger.LogDebug($"Loot has armor class: {lootArmor.ArmorClass}");
                    // Logger.LogDebug($"Loot has durability: {lootArmor.Repairable.Durability}");
                    // Logger.LogDebug($"Loot has max durability: {lootArmor.Repairable.MaxDurability}");
                    if (foundBetterArmor)
                    {
                        this.currentBodyArmorClass = lootArmor.ArmorClass;
                    }
                }



                return foundBetterArmor;
            }

            public bool shouldSwapGear(Item equipped, Item itemToLoot)
            {
                bool foundBiggerContainer = false;
                // If the item is a container, calculate the size and see if its bigger than what is equipped
                if (equipped.IsContainer)
                {
                    int equippedSize = getContainerSize(equipped as SearchableItemClass);
                    // Logger.LogDebug($"Current equipment has size: {equippedSize}");
                    int itemToLootSize = getContainerSize(itemToLoot as SearchableItemClass);
                    // Logger.LogDebug($"Item to loot has size: {itemToLootSize}");
                    foundBiggerContainer = equippedSize < itemToLootSize;
                }

                bool foundBetterArmor = considerArmorClass(equipped, itemToLoot);
                ArmorComponent lootArmor = itemToLoot.GetItemComponent<ArmorComponent>();
                ArmorComponent equippedArmor = equipped.GetItemComponent<ArmorComponent>();

                // Equip if we found item with a better item class.
                // Equip if we found an item with more slots only if what we have equipped is the same or worse armor class
                return foundBetterArmor || (foundBiggerContainer && (equippedArmor == null || equippedArmor.ArmorClass <= lootArmor.ArmorClass));
            }

            public void dropAndEquip(Item toDrop, Item toEquip)
            {
                Logger.LogWarning($"Throwing item: {toDrop}");
                botInventoryController.ThrowItem(toDrop, null, new Callback((IResult result) => tryAddItemsToBot(new Item[1] { toEquip })), false);
            }

            public void moveItem(Item item, ItemAddress place)
            {
                GStruct322<GClass2438> value = GClass2426.Move(item, place, botInventoryController, true);
                botInventoryController.TryRunNetworkTransaction(value, null);
            }

            public bool betterEquipmentCheck(Item itemToCheck)
            {
                // TODO: Try to combine this into one call to get all 4 slots 
                Item helmet = botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem;
                Item chest = botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.ArmorVest).ContainedItem;
                Item tacVest = botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem;
                Item backpack = botInventoryController.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem;

                string lootID = itemToCheck?.Parent?.Container?.ID;
                // Logger.LogDebug($"itemToCheck: {lootID}");
                // Logger.LogDebug($"itemToCheck: {itemToCheck?.Parent?.Item?.Name?.Localized()}");

                if (backpack != null && backpack.Parent.Container.ID == lootID && shouldSwapGear(backpack, itemToCheck))
                {
                    dropAndEquip(backpack, itemToCheck);
                    return true;
                }
                else if (helmet != null && helmet.Parent.Container.ID == lootID && shouldSwapGear(helmet, itemToCheck))
                {
                    dropAndEquip(helmet, itemToCheck);
                    return true;
                }
                else if (chest != null && chest.Parent.Container.ID == lootID && shouldSwapGear(chest, itemToCheck))
                {
                    dropAndEquip(chest, itemToCheck);
                    return true;
                }
                else if (tacVest != null && tacVest.Parent.Container.ID == lootID && shouldSwapGear(tacVest, itemToCheck))
                {
                    if (considerArmorClass(tacVest, itemToCheck) && chest != null)
                    {
                        botInventoryController.ThrowItem(chest, null, new Callback((IResult result) => dropAndEquip(tacVest, itemToCheck)), false);
                    }
                    else
                    {
                        dropAndEquip(tacVest, itemToCheck);
                    }
                    return true;
                }

                return false;
            }

            public void tryAddItemsToBot(Item[] items)
            {
                foreach (Item item in items)
                {
                    if (item != null && item.Name != null)
                    {
                        Logger.LogDebug($"Loot found on corpse: {item.Name.Localized()}");

                        if (betterEquipmentCheck(item))
                        {
                            continue;
                        }

                        if (!item.IsUnremovable)
                        {
                            GClass2419 ableToEquip = botInventoryController.FindSlotToPickUp(item);
                            if (ableToEquip != null)
                            {
                                Logger.LogWarning($"Someone is equipping: {item.Name.Localized()}");
                                moveItem(item, ableToEquip);
                                continue;
                            }
                            // else
                            // {
                            //     Logger.LogDebug($"Cannot equip: {item.Name.Localized()}");
                            // }
                        }

                        GClass2421 ableToPickUp = botInventoryController.FindGridToPickUp(item, botInventoryController);

                        if (ableToPickUp != null)
                        {
                            Logger.LogWarning($"Someone is picking up: {item.Name.Localized()}");
                            moveItem(item, ableToPickUp);
                            continue;
                        }
                        // else
                        // {
                        //     Logger.LogDebug($"No valid slot found for: {item.Name.Localized()}");
                        // }

                        Item[] nestedItems = item.GetAllItems().ToArray();
                        if (nestedItems.Length > 1)
                        {

                            // Logger.LogDebug($"Searching through {nestedItems.Length} items in: {item.Name.Localized()}");

                            Item[] containerItems = nestedItems.Where(nestedItem => nestedItem.Id != item.Id).ToArray();

                            if (containerItems.Length > 0)
                            {
                                tryAddItemsToBot(containerItems);
                            }
                        }
                    }
                    // else
                    // {
                    //     Logger.LogDebug("Item was null");
                    // }
                }
            }
        }
    }
}
