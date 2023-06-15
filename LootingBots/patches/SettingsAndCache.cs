using System.Reflection;

using Aki.Reflection.Patching;

using EFT;

using LootingBots.Patch.Util;

namespace LootingBots.Patch
{
    public class SettingsAndCachePatch
    {
        public void Enable()
        {
            new CleanCacheOnRaidEnd().Enable();
            new EnableWeaponSwitching().Enable();
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

    /* Patch that enables all bots to be able to switch weapons. Values based on Usec/Bear bot values */
    public class EnableWeaponSwitching : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotDifficultySettingsClass).GetMethod("ApplyPresetLocation");
        }

        [PatchPostfix]
        private static void PatchPostfix(
            BotLocationModifier modifier,
            ref BotDifficultySettingsClass __instance,
            ref WildSpawnType ___wildSpawnType_0
        )
        {
            bool corpseLootEnabled = LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(
                ___wildSpawnType_0
            );
            bool containerLootEnabled = LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(
                ___wildSpawnType_0
            );
            bool itemLootEnabled = LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(
                ___wildSpawnType_0
            );

            if (corpseLootEnabled || containerLootEnabled || itemLootEnabled)
            {
                __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON = 80;
                __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON_WITH_HELMET = 40;
            }
        }
    }
}
