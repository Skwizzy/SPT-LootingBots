using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace LootingBots.Patches;

/// <summary>
/// Used in prioritizing looting corpses killed by a bot
/// </summary>
public class InvokeOnKillPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(LocalPlayer).GetMethod(nameof(LocalPlayer.OnBeenKilledByAggressor));
    }

    [PatchPostfix]
    protected static void Postfix(
        LocalPlayer __instance,
        IPlayer aggressor,
        EFT.Ballistics.DamageInfo damageInfo,
        EBodyPart bodyPart,
        EDamageType lethalDamageType
    )
    {
        // Skip if the aggressor is a human player
        var aggressorBotOwner = aggressor.AIData?.BotOwner;
        if (aggressorBotOwner == null)
        {
            return;
        }

        // Call KillTarget where OnKillTarget invokes which we can subscribe to,
        // where the aggressor's loot finder will prioritize the victim's corpse
        aggressorBotOwner.BotPersonalStats.KillTarget(__instance.ProfileId, damageInfo);
    }
}
