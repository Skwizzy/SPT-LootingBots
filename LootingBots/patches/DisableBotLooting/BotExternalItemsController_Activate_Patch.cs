using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using SPT.Reflection.Patching;

namespace LootingBots.Patch.DisableBotLooting
{
    internal class BotExternalItemsController_Activate_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotExternalItemsController).GetMethod(nameof(BotExternalItemsController.Activate));
        }

        [PatchTranspiler]
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            // Create a new set of instructions
            List<CodeInstruction> newInstructionsList =
            [
                new CodeInstruction(OpCodes.Ret) // Return immediately
            ];

            return newInstructionsList;
        }
    }
}
