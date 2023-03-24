using System;

namespace LootingBots.Patch.Util
{
    [Flags]
    public enum BotType
    {
        Scav = 1,
        Pmc = 2,
        Raider = 4,
        Cultist = 8,
        Boss = 16,
        Follower = 32,

        All = Scav | Pmc | Raider | Cultist | Boss | Follower
    }

    public static class BotTypeUtils
    {
        public static bool hasScav(this BotType botType)
        {
            return botType.HasFlag(BotType.Scav);
        }

        public static bool hasPmc(this BotType botType)
        {
            return botType.HasFlag(BotType.Pmc);
        }

        public static bool hasRaider(this BotType botType)
        {
            return botType.HasFlag(BotType.Raider);
        }

        public static bool hasCultist(this BotType botType)
        {
            return botType.HasFlag(BotType.Cultist);
        }

        public static bool hasBoss(this BotType botType)
        {
            return botType.HasFlag(BotType.Boss);
        }

        public static bool hasFollower(this BotType botType)
        {
            return botType.HasFlag(BotType.Follower);
        }
    }
}
