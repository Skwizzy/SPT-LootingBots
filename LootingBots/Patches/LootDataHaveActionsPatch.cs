using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace LootingBots.Patches
{
    public class LootDataHaveActionsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(PatrolLootPointsData).GetMethod(nameof(PatrolLootPointsData.HaveActions), [typeof(Vector3), typeof(float), typeof(int)]);
        }

        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpile()
        {
            return [new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret)];
        }
    }
}
