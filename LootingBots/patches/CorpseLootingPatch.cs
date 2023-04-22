using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;

namespace LootingBots.Patch
{
    public class CorpseLootingPatch : ModulePatch
    {
        public static Log Log;

        public CorpseLootingPatch()
        {
            Log = LootingBots.LootLog;
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
                !LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                    ___botOwner_0.Profile.Info.Settings.Role
                )
            )
            {
                return true;
            }

            try
            {
                LootCorpse(___botOwner_0, ___gclass264_0);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e.StackTrace);
            }
            return true;
        }

        public static async void LootCorpse(BotOwner botOwner, GClass264 corpse)
        {
            BotLootData lootData = LootCache.GetLootData(botOwner.Id);

            // Initialize corpse inventory controller
            Player corpsePlayer = corpse.Player;
            Type corpseType = corpsePlayer.GetType();
            FieldInfo corpseInventory = corpseType.BaseType.GetField(
                "_inventoryController",
                BindingFlags.NonPublic
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.Instance
            );
            InventoryControllerClass corpseInventoryController = (InventoryControllerClass)
                corpseInventory.GetValue(corpse);

            Log.LogWarning(
                $"({botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} is looting corpse: ({corpsePlayer.Profile?.Info?.Settings?.Role}) {corpsePlayer.Profile?.Info.Nickname}"
            );

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

            await lootData.LootFinder.ItemAdder.TryAddItemsToBot(priorityItems);

            // After all equipment looting is done, attempt to change to the bots "main" weapon. Order follows primary -> secondary -> holster
            Log.LogDebug("Changing to main wep");
            botOwner.WeaponManager.Selector.TakeMainWeapon();
        }
    }
}