using System.Reflection;

using Aki.Reflection.Patching;

using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots.Patch
{
    public class LootSettingsPatch
    {
        public void Enable()
        {
            new EnableCorpseLootingPatch().Enable();
            new AddLootFinderPatch().Enable();
            new CleanCacheOnDeadPatch().Enable();
        }
    }

    public class EnableCorpseLootingPatch : ModulePatch
    {
        private static BotDifficultySettingsClass s_instance;

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
            s_instance = __instance;
            BotType enabledTypes = LootingBots.LootingEnabledBots.Value;
            if (enabledTypes.IsBotEnabled(___wildSpawnType_0))
            {
                EnableLooting();
            }
        }

        public static void EnableLooting()
        {
            float seeDist = LootingBots.BodySeeDist.Value;
            float leaveDist = LootingBots.BodyLeaveDist.Value;
            float lookPeriod = LootingBots.BodyLookPeriod.Value;

            s_instance.FileSettings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
            s_instance.FileSettings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
            s_instance.FileSettings.Patrol.DEAD_BODY_SEE_DIST = seeDist;
            s_instance.FileSettings.Patrol.DEAD_BODY_LEAVE_DIST = leaveDist;
            s_instance.FileSettings.Patrol.DEAD_BODY_LOOK_PERIOD = lookPeriod;
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
            lootFinder.BotOwner = __result;
            LootCache.BotDataCache.Add(player.Id, new BotLootData(lootFinder));
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
            LootingBots.ContainerLog.LogDebug(
                $"Bot {__instance.Id} is dead. Removing bot data from container cache."
            );
            LootCache.Destroy(__instance.Id);
        }
    }
}