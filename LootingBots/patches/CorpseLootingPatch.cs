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

                EquipmentSlot[] prioritySlots = GetPrioritySlots(___botOwner_0.Id);

                Item[] priorityItems = corpseInventoryController.Inventory.Equipment
                    .GetSlotsByName(prioritySlots)
                    .Select(slot => slot.ContainedItem)
                    .Where(item => item != null)
                    .ToArray();

                Log.LogWarning(
                    $"({___botOwner_0.Profile.Info.Settings.Role}) Bot {___botOwner_0.Id} is looting corpse: ({corpsePlayer.Profile?.Info?.Settings?.Role}) {corpsePlayer.Profile?.Info.Nickname}"
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

        static EquipmentSlot[] GetPrioritySlots(int botId)
        {
            BotLootData lootData = LootCache.GetLootData(botId);

            InventoryControllerClass botInventoryController =
                lootData.LootFinder.ItemAdder.GetInventoryController();
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
                Log.LogWarning($"Bot {botId} has backpack/rig and is looting weapons first!");
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
    }
}
