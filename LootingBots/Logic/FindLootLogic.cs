using DrakiaXYZ.BigBrain.Brains;
using EFT;
using LootingBots.Components;
using LootingBots.Utilities;

namespace LootingBots.Logic;

internal class FindLootLogic : CustomLogic
{
    private readonly LootingBrain _lootingBrain;
    private readonly LootFinder _lootFinder;
    private readonly BotLog _log;

    public FindLootLogic(BotOwner botOwner)
        : base(botOwner)
    {
        _lootingBrain = botOwner.GetPlayer.gameObject.GetComponent<LootingBrain>();
        _lootFinder = botOwner.GetPlayer.gameObject.GetComponent<LootFinder>();
        _log = new BotLog(LootingBots.LootLog, botOwner);

        if (botOwner.Profile.Nickname != _lootingBrain.BotOwner.Profile.Nickname)
        {
            _log.LogError(botOwner.Profile.Nickname + " is using the LootingBrain for " + _lootingBrain.BotOwner.Profile.Nickname);
        }
    }

    public override void Update(CustomLayer.ActionData data)
    {
        if (_lootFinder.IsScanRunning)
        {
            return;
        }

        // Do not scan if we don't have free space for loot, unless the bot is forced
        if (!_lootingBrain.HasFreeSpace && !_lootingBrain.ForceBrainEnabled)
        {
            return;
        }

        // Trigger a scan if one is not running already
        if (ScanScheduler.CanStartScan(out var ticket))
        {
            if (_log.DebugEnabled)
            {
                _log.LogDebug(
                    $"Starting scan ({ticket}) - HasFreeSpace: {_lootingBrain.HasFreeSpace}, IsScanRunning: {_lootFinder.IsScanRunning}, ForceBrainEnabled: {_lootingBrain.ForceBrainEnabled}"
                );
            }

            _lootFinder.BeginSearch(ticket);
        }
    }

    public override void Start()
    {
        _lootingBrain.UpdateGridStats();
    }

    public override void Stop()
    {
        _lootFinder.ResetScanTimer();
        _lootFinder.StopFindingLoot();
        base.Stop();
    }
}
