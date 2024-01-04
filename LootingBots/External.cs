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
    public static class External
    {
        public static bool ForceBotToLootNow(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            if (lootingBrain == null)
            {
                return false;
            }

            lootingBrain.ExternalLootScanRequest = true;
            lootingBrain.ExternalRequestExpiration = Time.time + duration;

            return true;
        }

        public static bool PreventBotFromLooting(BotOwner bot, float duration)
        {
            LootingBrain lootingBrain = bot.GetPlayer.gameObject.GetComponent<LootingBrain>();
            if (lootingBrain == null)
            {
                return false;
            }

            lootingBrain.ExternalLootScanInhibit = true;
            lootingBrain.ExternalRequestExpiration = Time.time + duration;

            return true;
        }
    }
}
