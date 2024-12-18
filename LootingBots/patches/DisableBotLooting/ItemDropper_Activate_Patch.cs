using System.Reflection;
using SPT.Reflection.Patching;

namespace LootingBots.Patch.DisableBotLooting
{
    internal class ItemDropper_Activate_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotItemDropper).GetMethod(nameof(BotItemDropper.Activate));
        }

        [PatchPrefix]
        private static bool PatchPrefix()
        {
            return false;
        }
    }
}
