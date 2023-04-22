using Aki.Reflection.Patching;
using UnityEngine;
using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;

namespace LootingBots.Patch
{
    public class LooseLootPatch
    {
        public void Enable()
        {
            new ManualUpdatePatch().Enable();
            new PickupLooseLootPatch().Enable();
            new HaveItemToTakePatch().Enable();
            new PickupActionPatch().Enable();
            new RemoveItemPatch().Enable();
        }
    }

    public class HaveItemToTakePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass454).GetMethod(
                "HaveItemToTake",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPostfix]
        private static void PatchPostfix(ref bool __result, ref BotOwner ___botOwner_0)
        {
            BotLootData botLootData = LootCache.GetLootData(___botOwner_0.Id);
            if (botLootData.ActiveItem && !__result)
            {
                __result = true;
            }
        }
    }

    public class ManualUpdatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass454).GetMethod(
                "ManualUpdate",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            ref LootItem ___lootItem_0,
            ref BotOwner ___botOwner_0,
            ref float ___float_7,
            ref float ___float_4,
            ref bool ___bool_0
        )
        {
            BotLootData botLootData = LootCache.GetLootData(___botOwner_0.Id);
            bool navigatingToItem = botLootData.ActiveItem != null && !___bool_0;

            if (navigatingToItem && ___float_7 < Time.time)
            {
                ___lootItem_0 = botLootData.ActiveItem;
                ___bool_0 = botLootData.LootFinder.IsCloseEnough(out float dist);

                if (!___bool_0)
                {
                    bool canMove = botLootData.LootFinder.TryMoveToLoot(ref ___float_7);
                    if (!canMove)
                    {
                        LootCache.Cleanup(ref ___botOwner_0, botLootData.ActiveItem.ItemId);
                        LootCache.AddNonNavigableLoot(
                            ___botOwner_0.Id,
                            botLootData.ActiveItem.ItemOwner.RootItem.Id
                        );
                        Logger.LogWarning(
                            $"Cannot navigate. Ignoring: {botLootData.ActiveItem.ItemOwner.RootItem.Name.Localized()}"
                        );
                        ___lootItem_0 = null;
                    }
                    else
                    {
                        botLootData.LootFinder.ShouldInteractDoor(dist);
                    }
                }
                else
                {
                    ___float_4 = Time.time + 1.5f;
                }

                return ___bool_0;
            }

            return !navigatingToItem;
        }
    }

    public class PickupLooseLootPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass454).GetMethod(
                "method_8",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            LootItem item,
            ref BotOwner ___botOwner_0,
            ref float ___float_5,
            ref GClass454 __instance
        )
        {
            BotLootData botLootData = LootCache.GetLootData(___botOwner_0.Id);
            if (botLootData.ActiveItem != null)
            {
                __instance.PickupAction(
                    ___botOwner_0.GetPlayer,
                    null,
                    botLootData.ActiveItem.ItemOwner.RootItem,
                    null
                );

                ___float_5 = Time.time + 4f;
                return false;
            }

            return true;
        }
    }

    public class PickupActionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass454).GetMethod(
                "PickupAction",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix(
            Player owner,
            GInterface265 possibleAction,
            Item rootItem,
            Player lootItemLastOwner
        )
        {
            BotLootData botLootData = LootCache.GetLootData(owner.Id);
            string itemId = botLootData.ActiveItem.ItemOwner.RootItem.Id;

            Action lootItemDel = botLootData.LootFinder.LootItem;
            owner.CurrentState.Pickup(true, new Action(lootItemDel));

            return false;
        }
    }

    public class RemoveItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass454).GetMethod(
                "method_2",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        [PatchPostfix]
        private static void PatchPostfix(
            ref BotOwner ___botOwner_0,
            ref LootItem ___lootItem_0,
            ref bool ___bool_0,
            LootItem item
        )
        {
            BotLootData botLootData = LootCache.GetLootData(___botOwner_0.Id);

            if (
                botLootData.ActiveItem != null
                && item.ItemOwner.RootItem.Id.Equals(botLootData.ActiveItem.ItemOwner.RootItem.Id)
            )
            {
                string itemId = item.ItemOwner.RootItem.Id;
                LootCache.Cleanup(ref ___botOwner_0, itemId);
                LootCache.AddVisitedLoot(___botOwner_0.Id, itemId);
                ___lootItem_0 = null;
                ___bool_0 = false;
                LootingBots.LootLog.LogWarning(
                    $"Removing successfully looted loose item: {item.ItemOwner.RootItem.Name.Localized()} ({itemId})"
                );
            }
        }
    }
}
