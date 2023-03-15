using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
            itemAdder = new ItemAdder(___botOwner_0);

            try
            {
                lootCorpse(___botOwner_0, ___gclass263_0);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }


                    // botOwner_0.WeaponManager.Selector.TryChangeWeapon() How to change weps

        public static async void lootCorpse(BotOwner ___botOwner_0, GClass263 ___gclass263_0)
        {
            Player corpse = ___gclass263_0.Player;
            Type corpseType = corpse.GetType();
            FieldInfo corpseInventory = corpseType.BaseType.GetField(
                "_inventoryController",
                BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.Instance
            );
            InventoryControllerClass corpseInventoryController = (InventoryControllerClass)
                corpseInventory.GetValue(corpse);

            logDebug($"{___botOwner_0.name} is looting corpse: {corpse.name}");

            Item[] priorityItems =
                corpseInventoryController.Inventory.Equipment
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
                    .Select(slot => slot.ContainedItem).ToArray();

            await itemAdder.tryAddItemsToBot(priorityItems);
        }

        public class ThrowEquipment
        {
            public Item toThrow;
            public Item toPickUp;
            public OnThrowCallback onThrowCallback;
        }

        public delegate Task OnThrowCallback();

        // TODO: When picking up guns, see if you can get them to switch weapons after equipping
        public class ItemAdder
        {
            public BotOwner botOwner_0;
            public InventoryControllerClass botInventoryController;

            // Represents the highest equipped armor class of the bot either from the armor vest or tac vest
            public int currentBodyArmorClass = 0;

            public ItemAdder(BotOwner botOwner_0)
            {
                try
                {
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
                        ThrowEquipment betterItem = betterEquipmentCheck(item);
                        if (betterItem.toThrow != null)
                        {
                            await throwAndEquip(
                                betterItem.toThrow,
                                betterItem.toPickUp,
                                betterItem.onThrowCallback
                            );
                            continue;
                        }

                        GClass2419 ableToEquip = botInventoryController.FindSlotToPickUp(item);
                        if (ableToEquip != null)
                        {
                            logWarning($"{botOwner_0.name} is equipping: {item.Name.Localized()}");
                            await moveItem(item, ableToEquip);
                            continue;
                        }
                        else
                        {
                            logDebug($"Cannot equip: {item.Name.Localized()}");
                        }

                        GClass2421 ableToPickUp = botInventoryController.FindGridToPickUp(
                            item,
                            botInventoryController
                        );

                        if (ableToPickUp != null)
                        {
                            logWarning($"{botOwner_0.name} is picking up: {item.Name.Localized()}");
                            await moveItem(item, ableToPickUp);
                            continue;
                        }
                        else
                        {
                            logDebug($"No valid slot found for: {item.Name.Localized()}");
                        }

                        Item[] nestedItems = item.GetAllItems().ToArray();
                        if (nestedItems.Length > 1)
                        {
                            Item[] containerItems = nestedItems
                                .Where(nestedItem => nestedItem.Id != item.Id)
                                .ToArray();

                            if (containerItems.Length > 0)
                            {
                                await tryAddItemsToBot(containerItems);
                            }
                        }
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
            public ThrowEquipment betterEquipmentCheck(Item itemToCheck)
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

                string lootID = itemToCheck?.Parent?.Container?.ID;
                ThrowEquipment equipment = new ThrowEquipment();

                if (
                    backpack?.Parent?.Container.ID == lootID
                    && shouldSwapGear(backpack, itemToCheck)
                )
                {
                    equipment.toThrow = backpack;
                    equipment.toPickUp = itemToCheck;
                }
                else if (
                    helmet?.Parent?.Container?.ID == lootID && shouldSwapGear(helmet, itemToCheck)
                )
                {
                    equipment.toThrow = helmet;
                    equipment.toPickUp = itemToCheck;
                }
                else if (
                    chest?.Parent?.Container?.ID == lootID && shouldSwapGear(chest, itemToCheck)
                )
                {
                    equipment.toThrow = chest;
                    equipment.toPickUp = itemToCheck;
                }
                else if (
                    tacVest?.Parent?.Container?.ID == lootID && shouldSwapGear(tacVest, itemToCheck)
                )
                {
                    if (considerArmorClass(tacVest, itemToCheck) && chest != null)
                    {
                        equipment.toThrow = chest;
                        equipment.onThrowCallback = async () =>
                            await throwAndEquip(tacVest, itemToCheck);
                    }
                    else
                    {
                        equipment.toThrow = tacVest;
                        equipment.toPickUp = itemToCheck;
                    }
                }

                return equipment;
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

                bool foundBetterArmor = considerArmorClass(equipped, itemToLoot);
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

            public Task throwAndEquip(
                Item toThrow,
                Item toEquip,
                OnThrowCallback onThrowCallback = null
            )
            {
                TaskCompletionSource<IResult> promise = new TaskCompletionSource<IResult>();

                logWarning($"Throwing item: {toThrow}");
                botInventoryController.ThrowItem(
                    toThrow,
                    null,
                    new Callback(
                        async (IResult result) =>
                        {
                            if (onThrowCallback != null)
                            {
                                await onThrowCallback();
                            }
                            else
                            {
                                await tryAddItemsToBot(new Item[1] { toEquip });
                            }
                            promise.TrySetResult(result);
                        }
                    ),
                    false
                );

                return Task.WhenAny(promise.Task);
            }

            public Task moveItem(Item item, ItemAddress place)
            {
                GStruct322<GClass2438> value = GClass2426.Move(
                    item,
                    place,
                    botInventoryController,
                    true
                );
                return botInventoryController.TryRunNetworkTransaction(value, null);
            }
        }
    }
}
