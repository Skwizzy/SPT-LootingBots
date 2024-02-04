using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

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

        public ExternalCommand(ExternalCommandType _type, float _duration = 0)
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
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();

            if (lootFinder == null || lootingBrain == null)
            {
                return false;
            }

            BotLog log = new BotLog(LootingBots.LootLog, bot);

            if (lootingBrain.HasFreeSpace)
            {
                log.LogWarning("Forcing a scan but bot does not have enough free space");
            }
            else
            {
                log.LogDebug("Forcing a loot scan");
            }

            lootFinder.ScanTimer = Time.time - 1f;
            lootFinder.LockUntilNextScan = true;
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

            BotLog log = new BotLog(LootingBots.LootLog, bot);
            log.LogDebug($"Preventing a bot from looting for the next {duration} seconds");

            lootFinder.ScanTimer = Time.time + duration;
            lootFinder.LockUntilNextScan = true;
            lootingBrain.DisableTransactions();
            return true;
        }
    }
}
