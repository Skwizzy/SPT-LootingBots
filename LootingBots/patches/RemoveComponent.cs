using System.Reflection;

using EFT;

using LootingBots.Patch.Components;

using SPT.Reflection.Patching;

namespace LootingBots.Patch
{
    internal class RemoveComponent : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsController).GetMethod(nameof(BotsController.Init), BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void PatchPostfix(BotsController __instance)
        {
            __instance.BotSpawner.OnBotRemoved += botOwner =>
            {
                if (botOwner.GetPlayer.TryGetComponent<LootingBrain>(out var component))
                {
                    UnityEngine.Object.Destroy(component);
                }
            };
        }
    }
}