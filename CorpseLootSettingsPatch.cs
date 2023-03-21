using Aki.Reflection.Patching;
using System.Reflection;
using EFT;

namespace LootingBots.Patch
{
    /** Disable discard limits (RMT protection) for all bots. Affects bots being able to drop items in raid */
    public class DisableDiscardLimitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass2401).GetMethod(
                "AffectsDiscardLimits",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            );
        }

        [PatchPrefix]
        private static bool PatchPrefix()
        {
            return false;
        }
    }

    public class CorpseLootSettingsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotDifficultySettingsClass).GetMethod("ApplyPresetLocation");
        }

        [PatchPostfix]
        private static void PatchPostfix(
            BotLocationModifier modifier,
            ref BotDifficultySettingsClass __instance,
            ref WildSpawnType ___wildSpawnType_0
        )
        {
            // Unchecked to get around cast of usec/bear WildSpawnType added in AkiBotsPrePatcher
            unchecked
            {
                WildSpawnType bear = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptBearValue;
                WildSpawnType usec = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptUsecValue;

                bool isPMC = ___wildSpawnType_0 == bear || ___wildSpawnType_0 == usec;

                if ((isPMC) && LootingBots.pmcLootingEnabled.Value)
                {
                    enableLooting(__instance);
                }
                else if (!isPMC)
                {
                    enableLooting(__instance);
                }
            }
        }

        public static void enableLooting(BotDifficultySettingsClass __instance)
        {
            float seeDist = LootingBots.bodySeeDist.Value;
            float leaveDist = LootingBots.bodyLeaveDist.Value;
            float lookPeriod = LootingBots.bodyLookPeriod.Value;

            __instance.FileSettings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
            __instance.FileSettings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
            __instance.FileSettings.Patrol.DEAD_BODY_SEE_DIST = seeDist;
            __instance.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST = leaveDist;
            __instance.FileSettings.Patrol.DEAD_BODY_LOOK_PERIOD = lookPeriod;
        }
    }
}
