using Aki.Reflection.Patching;
using System.Reflection;
using LootingBots.Patch.Util;
using Comfort.Common;
using EFT;

namespace LootingBots.Patch
{
    public class CorpseLootSettingsPatch : ModulePatch
    {
        private static BotDifficultySettingsClass instance;

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
            instance = __instance;
            BotType enabledTypes = LootingBots.lootingEnabledBots.Value;
            if (enabledTypes.isLootingEnabled(___wildSpawnType_0))
            {
                enableLooting();
            }
        }

        public static void enableLooting()
        {
            float seeDist = LootingBots.bodySeeDist.Value;
            float leaveDist = LootingBots.bodyLeaveDist.Value;
            float lookPeriod = LootingBots.bodyLookPeriod.Value;

            instance.FileSettings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
            instance.FileSettings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
            instance.FileSettings.Patrol.DEAD_BODY_SEE_DIST = seeDist;
            instance.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST = leaveDist;
            instance.FileSettings.Patrol.DEAD_BODY_LOOK_PERIOD = lookPeriod;
        }
    }
}
