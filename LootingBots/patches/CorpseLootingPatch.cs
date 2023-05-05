using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using System.Threading.Tasks;

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
                BotLootData lootData = LootCache.GetLootData(___botOwner_0.Id);

                // Initialize corpse inventory controller
                Player corpsePlayer = ___gclass264_0.Player;
                Type corpseType = corpsePlayer.GetType();
                FieldInfo corpseInventory = corpseType.BaseType.GetField(
                    "_inventoryController",
                    BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.Public
                        | BindingFlags.Instance
                );
                InventoryControllerClass corpseInventoryController = (InventoryControllerClass)
                    corpseInventory.GetValue(corpsePlayer);

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

                Log.LogWarning(
                    $"({___botOwner_0.Profile.Info.Settings.Role}) {___botOwner_0.Profile?.Info.Nickname.TrimEnd()} is looting corpse: ({corpsePlayer.Profile?.Info?.Settings?.Role}) {corpsePlayer.Profile?.Info.Nickname}"
                );
                lootData.LootFinder.LootItems(priorityItems);
                return false;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            return true;
        }
    }
}
