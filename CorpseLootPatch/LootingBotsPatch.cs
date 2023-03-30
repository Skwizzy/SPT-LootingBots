using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using EFT.Interactive;
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

    public class LootContainerPatch : ModulePatch
    {
        private static ItemAdder itemAdder;
        private static BotOwner botOwner;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(SitReservWay).GetMethod(
                "ComeTo",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPostfix]
        private static void PatchPostfix(
            BotOwner bot,
            ref bool ___ShallLoot,
            ref LootableContainer ___lootableContainer_0,
            ref bool ___bool_2
        )
        {
            if (___ShallLoot && ___bool_2 && LootingBots.containerLootingEnabled.Value)
            {
                botOwner = bot;
                itemAdder = new ItemAdder(bot);
                LootingBots.log.logWarning($"{___lootableContainer_0.name}");
                lootContainer(___lootableContainer_0?.ItemOwner?.Items?.ToArray()[0]);
            }
        }

        public static async void lootContainer(Item container)
        {
            if (container != null)
            {
                LootingBots.log.logWarning(
                    $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
                );

                await itemAdder.lootNestedItems(container);
            }
        }
    }

    public class LootCorpsePatch : ModulePatch
    {
        private static ItemAdder itemAdder;

        private static ItemAppraiser itemAppraiser;
        private static Log log;

        public LootCorpsePatch()
        {
            log = LootingBots.log;
            itemAppraiser = LootingBots.itemAppraiser;
        }

        protected override MethodBase GetTargetMethod()
        {
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
                lootCorpse(___gclass263_0);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }

        public static async void lootCorpse(GClass263 ___gclass263_0)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

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
            InventoryControllerClass corpseInventoryController = (InventoryControllerClass)
                corpseInventory.GetValue(corpse);

            log.logWarning(
                $"({itemAdder.botOwner_0.Profile.Info.Settings.Role}) {itemAdder.botOwner_0.Profile?.Info.Nickname.TrimEnd()} is looting corpse: ({corpse.Profile?.Info?.Settings?.Role}) {corpse.Profile?.Info.Nickname}"
            );

            // Calculate bots initial gear value
            itemAdder.calculateGearValue();

            Item[] priorityItems = corpseInventoryController.Inventory.Equipment
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

            watch.Stop();
            log.logDebug(
                $"Total time spent looting (s): {(float)(watch.ElapsedMilliseconds / 1000f)}"
            );
        }
    }
}
