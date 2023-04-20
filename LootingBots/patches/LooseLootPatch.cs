using Aki.Reflection.Patching;
using System.Reflection;
using LootingBots.Patch.Util;
using EFT.Interactive;
using EFT;
using UnityEngine;
using EFT.InventoryLogic;
using System;

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
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);
            if (botLootData.activeItem && !__result)
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
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);
            bool navigatingToItem = botLootData.activeItem != null && !___bool_0;

            if (navigatingToItem && ___float_7 < Time.time)
            {
                float dist;
                ___lootItem_0 = botLootData.activeItem;
                ___bool_0 = botLootData.lootFinder.isCloseEnough(out dist);

                if (!___bool_0)
                {
                    bool canMove = botLootData.lootFinder.tryMoveToLoot(ref ___float_7);
                    if (!canMove)
                    {
                        LootCache.cleanup(ref ___botOwner_0, botLootData.activeItem.ItemId);
                        LootCache.addNonNavigableLoot(
                            ___botOwner_0.Id,
                            botLootData.activeItem.ItemOwner.RootItem.Id
                        );
                        Logger.LogWarning(
                            $"Cannot navigate. Ignoring: {botLootData.activeItem.ItemOwner.RootItem.Name.Localized()}"
                        );
                        ___lootItem_0 = null;
                    }
                    else
                    {
                        botLootData.lootFinder.shouldInteractDoor(dist);
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
            ref LootItem ___lootItem_0,
            ref bool ___bool_0,
            ref float ___float_5,
            ref GClass454 __instance
        )
        {
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);
            if (botLootData.activeItem != null)
            {
                __instance.PickupAction(
                    ___botOwner_0.GetPlayer,
                    null,
                    botLootData.activeItem.ItemOwner.RootItem,
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
            Player lootItemLastOwner,
            ref GClass454 __instance
        )
        {
            BotLootData botLootData = LootCache.getLootData(owner.Id);
            string itemId = botLootData.activeItem.ItemOwner.RootItem.Id;

            Action lootItemDel = botLootData.lootFinder.lootItem;
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
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);

            if (
                botLootData.activeItem != null
                && item.ItemOwner.RootItem.Id.Equals(botLootData.activeItem.ItemOwner.RootItem.Id)
            )
            {
                string itemId = item.ItemOwner.RootItem.Id;
                LootCache.cleanup(ref ___botOwner_0, itemId);
                LootCache.addVisitedLoot(___botOwner_0.Id, itemId);
                ___lootItem_0 = null;
                ___bool_0 = false;
                LootingBots.containerLog.logWarning(
                    $"Removing successfully looted loose item: {item.ItemOwner.RootItem.Name.Localized()} ({itemId})"
                );
            }
        }
    }
}
