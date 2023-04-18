using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using EFT;
using LootingBots.Patch.Components;


namespace LootingBots.Patch
{
    public class CorpseLootingPatch : ModulePatch
    {
        private static ItemAdder itemAdder;

        private static ItemAppraiser itemAppraiser;
        private static Log log;

        public CorpseLootingPatch()
        {
            log = LootingBots.lootLog;
            itemAppraiser = LootingBots.itemAppraiser;
        }

        protected override MethodBase GetTargetMethod()
        {
            // GClass325 => BotOwner.DeadBodyWork
            return typeof(GClass326).GetMethod(
                "method_6",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref BotOwner ___botOwner_0, ref GClass264 ___gclass264_0)
        {
            // If the bot does not have looting enabled, do not override the method
            if (
                !LootingBots.lootingEnabledBots.Value.isBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
            )
            {
                return true;
            }

            itemAdder = new ItemAdder(___botOwner_0);

            try
            {
                lootCorpse(___gclass264_0);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }

        public static async void lootCorpse(GClass264 ___gclass264_0)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Initialize corpse inventory controller
            Player corpse = ___gclass264_0.Player;
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
                        EquipmentSlot.Holster,
                        EquipmentSlot.FirstPrimaryWeapon,
                        EquipmentSlot.SecondPrimaryWeapon,
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
