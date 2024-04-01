using System;
using System.Collections.Generic;

using EFT;

using LootingBots.Patch.Components;

namespace LootingBots.Patch.Util
{
    [Flags]
    public enum BotType
    {
        Scav = 1,
        Pmc = 2,
        PlayerScav = 4,
        Raider = 8,
        Cultist = 16,
        Boss = 32,
        Follower = 64,
        Bloodhound = 128,

        All = Scav | Pmc | PlayerScav | Raider | Cultist | Boss | Follower | Bloodhound
    }

    public static class BotTypeUtils
    {
        public static bool HasScav(this BotType botType)
        {
            return botType.HasFlag(BotType.Scav);
        }

        public static bool HasPmc(this BotType botType)
        {
            return botType.HasFlag(BotType.Pmc);
        }

        public static bool HasPlayerScav(this BotType botType)
        {
            return botType.HasFlag(BotType.PlayerScav);
        }

        public static bool HasRaider(this BotType botType)
        {
            return botType.HasFlag(BotType.Raider);
        }

        public static bool HasCultist(this BotType botType)
        {
            return botType.HasFlag(BotType.Cultist);
        }

        public static bool HasBoss(this BotType botType)
        {
            return botType.HasFlag(BotType.Boss);
        }

        public static bool HasFollower(this BotType botType)
        {
            return botType.HasFlag(BotType.Follower);
        }

        public static bool HasBloodhound(this BotType botType)
        {
            return botType.HasFlag(BotType.Bloodhound);
        }

        public static bool IsBotEnabled(this BotType enabledTypes, LootingBrain brain)
        {
            if (brain.IsPlayerScav)
            {
                return enabledTypes.HasPlayerScav();
            }
            WildSpawnType role = brain.BotOwner.Profile.Info.Settings.Role;
            return IsBotEnabled(enabledTypes, role);
        }

        public static bool IsBotEnabled(this BotType enabledTypes, WildSpawnType botType)
        {
            if (IsPMC(botType))
            {
                return enabledTypes.HasPmc();
            }

            if (IsBoss(botType))
            {
                return enabledTypes.HasBoss();
            }

            switch (botType)
            {
                case WildSpawnType.assault:
                case WildSpawnType.assaultGroup:
                {
                    return enabledTypes.HasScav();
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
                case WildSpawnType.followerBoar:
                {
                    return enabledTypes.HasFollower();
                }
                case WildSpawnType.exUsec:
                case WildSpawnType.pmcBot:
                {
                    return enabledTypes.HasRaider();
                }
                case WildSpawnType.sectantPriest:
                case WildSpawnType.sectantWarrior:
                case WildSpawnType.cursedAssault:
                {
                    return enabledTypes.HasCultist();
                }
                case WildSpawnType.arenaFighter:
                case WildSpawnType.arenaFighterEvent:
                case WildSpawnType.crazyAssaultEvent:
                {
                    return enabledTypes.HasBloodhound();
                }
                default:
                    return false;
            }
        }

        public static bool IsPMC(WildSpawnType wildSpawnType)
        {
            // Unchecked to get around cast of usec/bear WildSpawnType added in AkiBotsPrePatcher
            unchecked
            {
                WildSpawnType bear = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptBearValue;
                WildSpawnType usec = (WildSpawnType)Aki.PrePatch.AkiBotsPrePatcher.sptUsecValue;

                return wildSpawnType == bear || wildSpawnType == usec;
            }
        }

        public static bool IsScav(WildSpawnType wildSpawnType)
        {
            return wildSpawnType == WildSpawnType.assault
                || wildSpawnType == WildSpawnType.assaultGroup;
        }

        public static bool IsBoss(WildSpawnType wildSpawnType)
        {
            List<WildSpawnType> bosses = new List<WildSpawnType>
            {
                WildSpawnType.bossBully,
                WildSpawnType.bossGluhar,
                WildSpawnType.bossKilla,
                WildSpawnType.bossKnight,
                WildSpawnType.bossKojaniy,
                WildSpawnType.bossSanitar,
                WildSpawnType.bossTagilla,
                WildSpawnType.bossTest,
                WildSpawnType.bossZryachiy,
                WildSpawnType.bossBoar,
                WildSpawnType.bossBoarSniper
            };
            return bosses.Contains(wildSpawnType);
        }
    }
}
