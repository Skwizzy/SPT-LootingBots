using Aki.Reflection.Patching;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using LootingBots.Patch.Util;

namespace LootingBots.Patch
{
    public class ReservePatrolContainerPatch : ModulePatch
    {
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
            // if (___ShallLoot && ___bool_2 && LootingBots.ContainerLootingEnabled.Value)
            // {
            //     LootingBots.LootLog.LogWarning($"Reserve patrol looting: {___lootableContainer_0.name}");
            //     LootContainer(bot, ___lootableContainer_0?.ItemOwner?.Items?.ToArray()[0]);
            // }
        }

        public static async void LootContainer(BotOwner botOwner, Item container)
        {
            if (container != null)
            {
                LootingBots.LootLog.LogDebug(
                    $"{botOwner.Profile.Info.Settings.Role}) {botOwner.Profile?.Info.Nickname.TrimEnd()} found container: {container.Name.Localized()}"
                );
                
                BotLootData lootData = LootCache.GetLootData(botOwner.Id);
                await lootData.LootFinder.ItemAdder.LootNestedItems(container);
            }
        }
    }
}