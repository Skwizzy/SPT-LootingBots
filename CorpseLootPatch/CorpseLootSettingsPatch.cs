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
            // Unchecked to get around cast of usec/bear WildSpawnType added in AkiBotsPrePatcher
            unchecked
            {
                WildSpawnType bear = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptBearValue;
                WildSpawnType usec = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptUsecValue;

                bool isPMC = ___wildSpawnType_0 == bear || ___wildSpawnType_0 == usec || ___wildSpawnType_0 == WildSpawnType.pmcBot;
                if ((isPMC) && enabledTypes.hasPmc())
                {
                    enableLooting();
                    return;
                }

                switch (___wildSpawnType_0)
                {
                    case WildSpawnType.assault:
                    case WildSpawnType.cursedAssault:
                    case WildSpawnType.assaultGroup:
                    {
                        if (enabledTypes.hasScav())
                        {
                            enableLooting();
                            Logger.LogInfo("Corpse looting enabled for scav");
                        }
                        break;
                    }
                    case WildSpawnType.bossBully:
                    case WildSpawnType.bossGluhar:
                    case WildSpawnType.bossKilla:
                    case WildSpawnType.bossKnight:
                    case WildSpawnType.bossKojaniy:
                    case WildSpawnType.bossSanitar:
                    case WildSpawnType.bossTagilla:
                    case WildSpawnType.bossTest:
                    case WildSpawnType.bossZryachiy:
                    {
                        if (enabledTypes.hasBoss())
                        {
                            enableLooting();
                        }
                        break;
                    }
                    case WildSpawnType.followerBigPipe:
                    case WildSpawnType.followerBirdEye:
                    case WildSpawnType.followerBully:
                    case WildSpawnType.followerGluharAssault:
                    case WildSpawnType.followerGluharScout:
                    case WildSpawnType.followerGluharSecurity:
                    case WildSpawnType.followerGluharSnipe:
                    case WildSpawnType.followerKojaniy:
                    case WildSpawnType.followerSanitar:
                    case WildSpawnType.followerTagilla:
                    case WildSpawnType.followerTest:
                    case WildSpawnType.followerZryachiy:
                    {
                        if (enabledTypes.hasFollower())
                        {
                            enableLooting();
                        }
                        break;
                    }
                    case WildSpawnType.exUsec:
                    {
                        if (enabledTypes.hasRaider()) {
                            enableLooting();
                        }
                        break;
                    }
                    case WildSpawnType.sectantPriest:
                    case WildSpawnType.sectantWarrior:
                    {
                        if (enabledTypes.hasCultist()) {
                            enableLooting();
                        }
                        break;
                    }
                }
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
