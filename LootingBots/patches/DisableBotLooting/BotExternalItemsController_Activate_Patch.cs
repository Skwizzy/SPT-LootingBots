using System.Reflection;
using SPT.Reflection.Patching;

namespace LootingBots.Patch.DisableBotLooting
{
    internal class BotExternalItemsController_Activate_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotExternalItemsController).GetMethod(nameof(BotExternalItemsController.Activate));
        }

        [PatchPrefix]
        private static bool PatchPrefix()
        {
            return false;
        }
    }
}
