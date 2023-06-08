using System.Reflection;

using Aki.Reflection.Patching;

using EFT;

using LootingBots.Patch.Util;

namespace LootingBots.Patch
{
    public class ResetActiveLootPatch
    {
        public void Enable()
        {
            new CleanCacheOnRaidEnd().Enable();
        }
    }

    public class CleanCacheOnRaidEnd : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(
                "Dispose",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            LootingBots.LootLog.LogDebug($"Resetting Loot Cache");
            ActiveLootCache.Reset();
        }
    }
}
