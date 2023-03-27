using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using EFT.Interactive;
using EFT;
using Comfort.Common;
using UnityEngine;

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

    // public class LootContainerPatch1 : ModulePatch
    // {
    //     private static ItemAdder itemAdder;

    //     protected override MethodBase GetTargetMethod()
    //     {
    //         return typeof(BotOwner).GetMethod(
    //             "PreActivate",
    //             BindingFlags.Public | BindingFlags.Instance
    //         );
    //     }

    //     [PatchPostfix]
    //     private static void PatchPostfix(
    //         ref BotOwner __instance,
    //         BotZone zone,
    //         GameTimeClass time,
    //         BotGroupClass group,
    //         bool autoActivate = true
    //     )
    //     {
    //         itemAdder = new ItemAdder(__instance);
    //         __instance.PatrollingData.OnPatrolPointCome += onPatrolPointCome;
    //     }

    //     public static async void onPatrolPointCome(GClass495 obj)
    //     {
    //         if (obj.TargetPoint.name.Contains("loot"))
    //         {
    //             Logger.LogDebug("Method 1");
    //             Logger.LogDebug($"{obj.TargetPoint}");
    //             Logger.LogDebug($"{obj.TargetPoint.position}");
    //             Collider[] array = Physics.OverlapSphere(
    //                 obj.TargetPoint.position,
    //                 1f,
    //                 LayerMask.GetMask(
    //                     new string[]
    //                     {
    //                         "Interactive",
    //                         // "Deadbody",
    //                         "Loot"
    //                     }
    //                 ),
    //                 QueryTriggerInteraction.Collide
    //             );

    //             foreach (Collider collider in array)
    //             {
    //                 LootableContainer containerObj =
    //                     collider.gameObject.GetComponentInParent<LootableContainer>();
    //                 if (containerObj)
    //                 {
    //                     Item container = containerObj.ItemOwner.Items.ToArray()[0];

    //                     if (container != null)
    //                     {
    //                         Logger.LogWarning($"Found container: {container.Name.Localized()}");
    //                         Logger.LogDebug(containerObj.ItemOwner.Items.ToArray().Length);

    //                         await itemAdder.tryAddItemsToBot(
    //                             container
    //                                 .GetAllItems()
    //                                 .Where(item => !item.IsUnremovable && item.Id != container.Id)
    //                                 .ToArray()
    //                         );
    //                     }
    //                 }
    //             }
    //         }
    //     }
    // }

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
                lootContainer(___lootableContainer_0?.ItemOwner?.Items?.ToArray()[0]);
            }
        }

        public static async void lootContainer(Item container)
        {
            if (container != null)
            {
                Logger.LogWarning(
                    $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
                );

                await itemAdder.lootNestedItems(container);
            }
        }
    }

    // public class LootContainerPatch1 : ModulePatch
    // {
    //     private static ItemAdder itemAdder;
    //     private static BotOwner botOwner;

    //     private static bool checkPoint;

    //     protected override MethodBase GetTargetMethod()
    //     {
    //         return typeof(GClass452).GetMethod(
    //             "RefreshClosestItems",
    //             BindingFlags.Public | BindingFlags.Instance
    //         );
    //     }

    //     [PatchPrefix]
    //     private static void PatchPrefix(ref BotOwner ___botOwner_0, ref float ___float_6)
    //     {
    //         if (___float_6 < Time.time)
    //         {
    //             Collider[] array = Physics.OverlapSphere(
    //                 ___botOwner_0.Position,
    //                 25f,
    //                 LayerMask.GetMask(
    //                     new string[]
    //                     {
    //                         "Interactive",
    //                         // "Deadbody",
    //                         // "Loot"
    //                     }
    //                 ),
    //                 QueryTriggerInteraction.Collide
    //             );

    //             foreach (Collider collider in array)
    //         {
    //             LootableContainer containerObj =
    //                 collider.gameObject.GetComponentInParent<LootableContainer>();
    //             if (containerObj)
    //             {
    //                 Item container = containerObj.ItemOwner.Items.ToArray()[0];

    //                 if (container != null)
    //                 {
    //                     Logger.LogWarning(
    //                         $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
    //                     );
    //                     Logger.LogDebug(containerObj.ItemOwner.Items.ToArray().Length);
    //                 }
    //             }
    //         }
    //         }
    //     }

    //     [PatchPostfix]
    //     private static void PatchPostfix(
    //         ref GClass428 ___gclass428_0,
    //         ref BotOwner ___botOwner_0,
    //         ref float ___float_0
    //     )
    //     {
    //         // if (___gclass428_0.Status != PatrolStatus.stay)
    //         // {
    //         //     return;
    //         // }

    //         if (checkPoint)
    //         {
    //             botOwner = ___botOwner_0;
    //             checkPoint = false;
    //             itemAdder = new ItemAdder(___botOwner_0);
    //             Logger.LogDebug($"{___gclass428_0.CurPatrolPoint.TargetPoint.gameObject.name}");
    //             Logger.LogDebug($"{___gclass428_0.CurPatrolPoint.TargetPoint.position}");
    //             Logger.LogDebug($"{___gclass428_0.CurTargetPoint}");
    //             Logger.LogDebug(
    //                 $"{___gclass428_0.CurPatrolPoint.TargetPoint.GetComponentInParent<LootPoint>()}"
    //             );
    //             Logger.LogDebug(
    //                 $"{___gclass428_0.CurPatrolPoint.TargetPoint.gameObject.GetComponentInParent<LootableContainer>()}"
    //             );

    //             // if (!___gclass428_0.CurPatrolPoint.TargetPoint.name.Contains("GameObject"))
    //             // {
    //             getContainerAtPoint(___gclass428_0.CurPatrolPoint.TargetPoint.position);
    //             // }
    //         }
    //     }

    //     public static async void getContainerAtPoint(Vector3 obj)
    //     {
    //         Collider[] array = Physics.OverlapSphere(
    //             obj,
    //             1f,
    //             LayerMask.GetMask(
    //                 new string[]
    //                 {
    //                     "Interactive",
    //                     // "Deadbody",
    //                     "Loot"
    //                 }
    //             ),
    //             QueryTriggerInteraction.Collide
    //         );

    //         Logger.LogDebug("Collider generated");

    //         foreach (Collider collider in array)
    //         {
    //             LootableContainer containerObj =
    //                 collider.gameObject.GetComponentInParent<LootableContainer>();
    //             if (containerObj)
    //             {
    //                 Item container = containerObj.ItemOwner.Items.ToArray()[0];

    //                 if (container != null)
    //                 {
    //                     Logger.LogWarning(
    //                         $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
    //                     );
    //                     Logger.LogDebug(containerObj.ItemOwner.Items.ToArray().Length);

    //                     await itemAdder.lootNestedItems(container);
    //                 }
    //             }
    //         }
    //     }
    // }

    public class LootCorpsePatch : ModulePatch
    {
        private static MethodInfo _method_7;
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
