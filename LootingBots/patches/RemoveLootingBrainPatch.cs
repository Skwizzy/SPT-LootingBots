using System.Reflection;
using EFT;
using LootingBots.Patch.Components;
using LootingBots.Utilities;
using SPT.Reflection.Patching;

namespace LootingBots.Patches
{
    public class RemoveLootingBrainPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotOwner).GetMethod(nameof(BotOwner.Dispose), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix(BotOwner __instance)
        {
            if (__instance.GetPlayer.TryGetComponent<LootingBrain>(out var component))
            {
                UnityEngine.Object.Destroy(component);
            }

            if (LootingBots.LootLog.DebugEnabled)
            {
                LootingBots.LootLog.LogDebug("Cleanup on ActiveLootCache");
            }

            ActiveLootCache.Cleanup(__instance);
            ActiveBotCache.Remove(__instance);
        }
    }
}
