using System.Reflection;
using SPT.Reflection.Patching;

namespace LootingBots.Patch.DisableBotLooting
{
    internal class ItemTaker_Activate_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotItemTaker).GetMethod(nameof(BotItemTaker.Activate));
        }

        [PatchPrefix]
        private static bool PatchPrefix()
        {
            return false;
        }
    }
}
