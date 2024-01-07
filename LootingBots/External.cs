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
        public static bool ForceBotToLootNow(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            if (lootingBrain == null)
            {
                return false;
            }

            lootingBrain.CurrentExternalCommand = new ExternalCommand(ExternalCommandType.ForceLootScan, duration);
            return true;
        }

        public static bool PreventBotFromLooting(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            if (lootingBrain == null)
            {
                return false;
            }

            lootingBrain.CurrentExternalCommand = new ExternalCommand(ExternalCommandType.PreventLootScan, duration);
            return true;
        }
    }
}
