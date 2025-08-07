using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Components;
using LootingBots.Utilities;

namespace LootingBots.Logic
{
    internal class FindLootLogic : CustomLogic
    {
        private readonly LootingBrain _lootingBrain;
        private readonly LootFinder _lootFinder;
        readonly BotLog _log;

        public FindLootLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _lootingBrain = botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
            _lootFinder = botOwner.GetPlayer.gameObject.GetComponent<LootFinder>();
            _log = new BotLog(LootingBots.LootLog, botOwner);
        }

        public override void Update(CustomLayer.ActionData data)
        {
            if (!_lootingBrain.HasFreeSpace)
            {
                // Need to disable LockUntilNextScan if the bot has no free space to prevent an infinite looting loop
                _lootFinder.LockUntilNextScan = false;

                return;
            }

            // Trigger a scan if one is not running already
            if (!_lootFinder.IsScanRunning)
            {
                if (_log.DebugEnabled)
                {
                    _log.LogDebug(
                        $"Starting scan - free space: {_lootingBrain.HasFreeSpace}. isScanRunning: {_lootFinder.IsScanRunning}"
                    );
                }
                _lootFinder.BeginSearch();
            }
        }

        public override void Stop()
        {
            _lootFinder.IsScanRunning = false;
            _lootFinder.ResetScanTimer();
            _lootFinder.StopAllCoroutines();
            base.Stop();
        }
    }
}
