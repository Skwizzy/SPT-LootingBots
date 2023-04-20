using Aki.Reflection.Patching;
using System.Reflection;
using LootingBots.Patch.Util;
using EFT;
using UnityEngine;
using LootingBots.Patch.Components;

namespace LootingBots.Patch
{
    public class LootSettingsPatch {
        public void Enable() {
            new EnableCorpseLootingPatch().Enable();
            new AddLootFinderPatch().Enable();
            new CleanCacheOnDeadPatch().Enable();
        }
    }
    
    public class EnableCorpseLootingPatch : ModulePatch
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
            if (enabledTypes.isBotEnabled(___wildSpawnType_0))
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

    public class AddLootFinderPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BotOwner).GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
            );
        }

        [PatchPostfix]
        private static void PatchPostfix(
            ref BotOwner __result,
            Player player,
            GameObject behaviourTreePrefab,
            GameTimeClass gameDataTime,
            BotControllerClass botsController,
            bool isLocalGame
        )
        {
            LootFinder lootFinder = player.gameObject.AddComponent<LootFinder>();
            lootFinder.botOwner = __result;
            LootCache.botDataCache.Add(player.Id, new BotLootData(lootFinder));
        }
    }

    public class CleanCacheOnDeadPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(
                "OnDead",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(ref Player __instance)
        {
            LootingBots.containerLog.logDebug(
                $"Bot {__instance.Id} is dead. Removing bot data from container cache."
            );
            LootCache.destroy(__instance.Id);
        }
    }
}
