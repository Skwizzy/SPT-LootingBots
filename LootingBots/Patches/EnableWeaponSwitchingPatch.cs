using System.Reflection;
using EFT;
using LootingBots.Utilities;
using SPT.Reflection.Patching;

namespace LootingBots.Patches;

public class EnableWeaponSwitchingPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotSettings).GetMethod(nameof(BotSettings.ApplyPresetLocation));
    }

    [PatchPostfix]
    private static void PatchPostfix(
        BotLocationModifier modifier,
        ref BotSettings __instance,
        ref WildSpawnType ___WildSpawnType_0
    )
    {
        var corpseLootEnabled = LootingBots.CorpseLootingEnabled.Value.IsBotEnabled(___WildSpawnType_0);
        var containerLootEnabled = LootingBots.ContainerLootingEnabled.Value.IsBotEnabled(___WildSpawnType_0);
        var itemLootEnabled = LootingBots.LooseItemLootingEnabled.Value.IsBotEnabled(___WildSpawnType_0);

        if (corpseLootEnabled || containerLootEnabled || itemLootEnabled)
        {
            __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON = 80;
            __instance.FileSettings.Shoot.CHANCE_TO_CHANGE_WEAPON_WITH_HELMET = 40;
        }
    }
}
