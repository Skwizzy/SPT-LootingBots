using Aki.Reflection.Patching;
using System.Reflection;
using LootingBots.Patch.Util;
using EFT.Interactive;
using EFT;
using UnityEngine;

namespace LootingBots.Patch
{
    public class LooseLootPatch
    {
        public void Enable()
        {
            new ManualUpdatePatch().Enable();
            new PickupLooseLootPatch().Enable();
            new HaveItemToTakePatch().Enable();
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
            ref bool ___bool_0
        )
        {
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);
            // Logger.LogDebug("In manual update");

            if (botLootData.activeItem && ___float_7 < Time.time)
            {
                Logger.LogDebug("In manual update");
                float dist;
                ___lootItem_0 = botLootData.activeItem;
                ___bool_0 = botLootData.lootFinder.isCloseEnough(out dist);

                if (!___bool_0)
                {
                    bool canMove = botLootData.lootFinder.tryMoveToLoot(ref ___float_7);
                    if (!canMove)
                    {
                        LootCache.cleanup(ref ___botOwner_0, botLootData.activeItem.ItemId);
                        ___lootItem_0 = null;
                    }
                }

                return ___bool_0;
            }

            return true;
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
            ref bool ___bool_0
        )
        {
            BotLootData botLootData = LootCache.getLootData(___botOwner_0.Id);
            if (botLootData.activeItem)
            {
                botLootData.lootFinder.lootItem(botLootData.activeItem);
                LootCache.cleanup(ref ___botOwner_0, item.ItemOwner.RootItem.Id);
                ___lootItem_0 = null;
                ___bool_0 = false;
                return false;
            }

            return true;
        }
    }
}
