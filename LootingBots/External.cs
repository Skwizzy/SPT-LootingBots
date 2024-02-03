using EFT;

using LootingBots.Patch.Components;

using UnityEngine;

namespace LootingBots
{
    public enum ExternalCommandType
    {
        None,
        ForceLootScan,
        PreventLootScan,
    }

    public class ExternalCommand
    {
        public ExternalCommandType CommandType { get; private set; } = ExternalCommandType.None;
        public float Duration { get; private set; } = 0;
        public float Expiration { get; private set; } = 0;

        public ExternalCommand() { }

        public ExternalCommand(ExternalCommandType _type, float _duration)
        {
            CommandType = _type;
            Duration = _duration;
            Expiration = Time.time + _duration;
        }
    }

    public static class External
    {
        /** Forces a bot to scan for loot as soon as they are able to. */
        public static bool ForceBotToScanLoot(BotOwner bot)
        {
            LootFinder lootFinder = bot.GetPlayer.gameObject.GetComponent<LootFinder>();
            if (lootFinder == null)
            {
                return false;
            }

            lootFinder.ScanTimer = Time.time - 1f;

            return true;
        }

        /** Stops a bot from looting if it is currently looting something and prevents loot scans for the amount of seconds specified by duration */
        public static bool PreventBotFromLooting(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            LootFinder lootFinder = bot.GetPlayer.gameObject.GetComponent<LootFinder>();
            if (lootingBrain == null || lootFinder == null)
            {
                return false;
            }

            lootingBrain.DisableTransactions();
            lootFinder.ScanTimer = Time.time + duration;
            return true;
        }
    }
}
