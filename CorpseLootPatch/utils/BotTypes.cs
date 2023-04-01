using System;
using EFT;

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

        public static bool isLootingEnabled(this BotType botType, WildSpawnType spawnType)
        {
            BotType enabledTypes = LootingBots.lootingEnabledBots.Value;
            // Unchecked to get around cast of usec/bear WildSpawnType added in AkiBotsPrePatcher
            unchecked
            {
                WildSpawnType bear = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptBearValue;
                WildSpawnType usec = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptUsecValue;

                bool isPMC = spawnType == bear || spawnType == usec;
                if (isPMC)
                {
                    return enabledTypes.hasPmc();
                }

                switch (spawnType)
                {
                    case WildSpawnType.assault:
                    case WildSpawnType.assaultGroup:
                    {
                        return enabledTypes.hasScav();
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
                        return enabledTypes.hasBoss();
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
                        return enabledTypes.hasFollower();
                    }
                    case WildSpawnType.exUsec:
                    case WildSpawnType.pmcBot:
                    {
                        return enabledTypes.hasRaider();
                    }
                    case WildSpawnType.sectantPriest:
                    case WildSpawnType.sectantWarrior:
                    case WildSpawnType.cursedAssault:
                    {
                        return enabledTypes.hasCultist();
                    }
                    default:
                        return false;
                }
            }
        }
    }
}
