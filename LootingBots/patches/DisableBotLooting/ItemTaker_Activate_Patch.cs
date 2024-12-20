using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using SPT.Reflection.Patching;

namespace LootingBots.Patch.DisableBotLooting
{
    internal class ItemTaker_Activate_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotItemTaker).GetMethod(nameof(BotItemTaker.Activate));
        }

        [PatchTranspiler]
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            // Create a new set of instructions
            List<CodeInstruction> instructionsList =
            [
                new CodeInstruction(OpCodes.Ret) // Return immediately
            ];

            return instructionsList;
        }
    }
}
