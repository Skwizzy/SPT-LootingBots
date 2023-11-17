using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

namespace LootingBots.Brain.Logics
{
    internal class FindLootLogic : CustomLogic
    {
        private readonly LootingBrain _lootingBrain;
        private readonly LootFinder _lootFinder;

        public FindLootLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _lootingBrain = botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
            _lootFinder = botOwner.GetPlayer.gameObject.GetComponent<LootFinder>();
        }

        public override void Update()
        {
            // If the bot has more than the reserved amount of slots needed for ammo, trigger a loot scan
            if (_lootingBrain.Stats.AvailableGridSpaces > LootUtils.RESERVED_SLOT_COUNT)
            {
                _lootFinder.Start();
            }
        }
    }
}
