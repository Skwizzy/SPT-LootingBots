using System.Reflection;

using SPT.Reflection.Patching;

namespace LootingBots.patches.DisableBotLooting
{
    public class AICoreStrategyAbstractClass_method_0_Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(AICoreStrategyAbstractClass<BotLogicDecision>).GetMethod(nameof(AICoreStrategyAbstractClass<BotLogicDecision>.method_0));
        }

        [PatchPrefix]
        public static bool Prefix(int index, AICoreLayerClass<BotLogicDecision> layer, bool activeOnStart)
        {
            if (layer.Name() == "LootPatrol")
            {
                Logger.LogDebug("Disabling BSG LootPatrol brain");
                // Skip the original method
                return false;
            }

            return true;
        }
    }
}
