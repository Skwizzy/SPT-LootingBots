using Aki.Reflection.Patching;
using System.Reflection;
using EFT;

namespace LootingBots.Patch
{
    /** For Debugging */
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
            float seeDist = LootingBots.bodySeeDist.Value;
            float leaveDist = LootingBots.bodyLeaveDist.Value;
            float lookPeriod = LootingBots.bodyLookPeriod.Value;
            unchecked {
            WildSpawnType bear = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptBearValue;
            WildSpawnType usec = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptUsecValue;

            bool isPMC = ___wildSpawnType_0 == bear || ___wildSpawnType_0 == usec;

            if ((isPMC) && LootingBots.pmcLootingEnabled.Value)
            {
                Logger.LogDebug("Setting config for PMC");
                __instance.FileSettings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
                __instance.FileSettings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
                __instance.FileSettings.Patrol.DEAD_BODY_SEE_DIST = seeDist;
                __instance.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST = leaveDist;
                __instance.FileSettings.Patrol.DEAD_BODY_LOOK_PERIOD = lookPeriod;
            }
            else if (!isPMC)
            {
                Logger.LogDebug("Setting config for bot");
                __instance.FileSettings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
                __instance.FileSettings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
                __instance.FileSettings.Patrol.DEAD_BODY_SEE_DIST = seeDist;
                __instance.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST = leaveDist;
                __instance.FileSettings.Patrol.DEAD_BODY_LOOK_PERIOD = lookPeriod;
            }
            }
        }
    }
}
