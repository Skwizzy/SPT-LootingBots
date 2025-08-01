using System.Reflection;
using EFT;
using LootingBots.Utilities;
using SPT.Reflection.Patching;

namespace LootingBots.Patches
{
    public class EnableWeaponSwitchingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotDifficultySettingsClass).GetMethod(nameof(BotDifficultySettingsClass.ApplyPresetLocation));
        }

        [PatchPostfix]
        private static void PatchPostfix(
            BotLocationModifier modifier,
            ref BotDifficultySettingsClass __instance,
            ref WildSpawnType ___wildSpawnType_0
        )
        {
            bool corpseLootEnabled = LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(___wildSpawnType_0);
            bool containerLootEnabled = LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(___wildSpawnType_0);
            bool itemLootEnabled = LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(___wildSpawnType_0);

            if (corpseLootEnabled || containerLootEnabled || itemLootEnabled)
            {
                __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON = 80;
                __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON_WITH_HELMET = 40;
            }
        }
    }
}
