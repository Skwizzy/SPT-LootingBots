using Aki.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;
using EFT.Interactive;
using EFT;

namespace LootingBots.Patch {
   public class ReservePatrolContainerPatch : ModulePatch
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
                LootingBots.lootLog.logWarning($"Reserve patrol looting: {___lootableContainer_0.name}");
                lootContainer(___lootableContainer_0?.ItemOwner?.Items?.ToArray()[0]);
            }
        }

        public static async void lootContainer(Item container)
        {
            if (container != null)
            {
                LootingBots.lootLog.logDebug(
                    $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
                );

                await itemAdder.lootNestedItems(container);
            }
        }
    }
}