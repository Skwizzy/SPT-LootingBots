using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace LootingBots.Patches
{
    public class LootDataSetTargetLootClusterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(PatrolLootPointsData).GetMethod(nameof(PatrolLootPointsData.SetTargetLootCluster));
        }

        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpile()
        {
            return [new CodeInstruction(OpCodes.Ret)];
        }
    }
}
