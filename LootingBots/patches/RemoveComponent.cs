using Aki.Reflection.Patching;
using EFT;
using LootingBots.Patch.Components;
using System.Reflection;
using UnityEngine;

namespace LootingBots.Patch
{
    internal class RemoveComponent : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotsController).GetMethod(
                "Init",
                BindingFlags.Public | BindingFlags.Instance
            );
        }

        [PatchPostfix]
        private static void PatchPostfix(BotsController __instance)
        {
            __instance.BotSpawner.OnBotRemoved += botOwner =>
            {
                if (botOwner.GetPlayer.TryGetComponent<LootingBrain>(out var component))
                {
                    Object.Destroy(component);
                }
            };
        }
    }
}
