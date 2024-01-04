using EFT;
using LootingBots.Patch.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;

namespace LootingBots
{
    public enum ExternalCommand
    {
        None = 0,
        ForceLootScan = 1,
        PreventLootScan = 2,
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

            lootingBrain.CurrentExternalCommand = ExternalCommand.ForceLootScan;
            lootingBrain.ExternalCommandExpiration = Time.time + duration;

            return true;
        }

        public static bool PreventBotFromLooting(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            if (lootingBrain == null)
            {
                return false;
            }

            lootingBrain.CurrentExternalCommand = ExternalCommand.PreventLootScan;
            lootingBrain.ExternalCommandExpiration = Time.time + duration;

            return true;
        }
    }
}
