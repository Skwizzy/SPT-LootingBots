using System.Reflection;
using EFT;
using LootingBots.Utilities;
using SPT.Reflection.Patching;

namespace LootingBots.Patches
{
    public class CleanCacheOnRaidEndPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.Dispose), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            if (LootingBots.LootLog.DebugEnabled)
            {
                LootingBots.LootLog.LogDebug("Resetting Caches");
            }

            ActiveLootCache.Reset();
            ActiveBotCache.Reset();
        }
    }
}
