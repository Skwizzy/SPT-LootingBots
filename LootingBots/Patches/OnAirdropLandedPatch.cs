using System.Reflection;
using EFT.Interactive;
using SPT.Reflection.Patching;

namespace LootingBots.Patches;

public class OnAirdropLandedPatch : ModulePatch
{
    public static Action<LootableContainer> OnAirdropLanded;

    // InitializeLandedAirdrop in EFT.Airdrop.ClientAirDrop is called after the airdrop is landed, making it perfect for us to hook into
    protected override MethodBase GetTargetMethod()
    {
        return typeof(EFT.Airdrop.ClientAirDrop).GetMethod(nameof(EFT.Airdrop.ClientAirDrop.InitializeLandedAirdrop));
    }

    [PatchPostfix]
    public static void Postfix(EFT.Airdrop.ClientAirDrop __instance)
    {
        var lootableContainer = __instance._syncObject.GetComponentInChildren<LootableContainer>();

        if (OnAirdropLanded != null)
        {
            OnAirdropLanded(lootableContainer);
        }
    }
}
