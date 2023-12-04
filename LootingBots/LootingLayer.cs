using System;
using System.Text;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Brain.Logics;
using LootingBots.Patch.Components;
using LootingBots.Patch.Util;

using UnityEngine;

namespace LootingBots.Brain
{
    internal class LootingLayer : CustomLayer
    {
        private readonly LootingBrain _lootingBrain;
        private readonly LootFinder _lootFinder;

        private float _scanTimer;

        private bool IsScheduledScan
        {
            get { return _scanTimer < Time.time; }
        }

        readonly BotLog _log;

        public LootingLayer(BotOwner botOwner, int priority)
            : base(botOwner, priority)
        {
            LootingBrain lootingBrain = botOwner.GetPlayer.gameObject.AddComponent<LootingBrain>();
            LootFinder lootFinder = botOwner.GetPlayer.gameObject.AddComponent<LootFinder>();
            lootingBrain.Init(botOwner);
            lootFinder.Init(botOwner);

            _scanTimer = Time.time + LootingBots.InitialStartTimer.Value;
            _lootingBrain = lootingBrain;
            _lootFinder = lootFinder;
            _log = new BotLog(LootingBots.LootLog, botOwner);
        }

        public override string GetName()
        {
            return "Looting";
        }

        public override bool IsActive()
        {
            bool isBotActive = BotOwner.BotState == EBotState.Active;
            bool isNotHealing =
                !BotOwner.Medecine.FirstAid.Have2Do && !BotOwner.Medecine.SurgicalKit.HaveWork;
            return isBotActive
                && isNotHealing
                && _lootingBrain.IsBrainEnabled
                && (IsScheduledScan || _lootingBrain.IsBotLooting);
        }

        public override void Start()
        {
            _lootingBrain.EnableTransactions();
            _lootingBrain.UpdateGridStats();
            base.Start();
        }

        public override void Stop()
        {
            _lootingBrain.DisableTransactions();
            _lootingBrain.UpdateGridStats();
            base.Stop();
        }

        public override Action GetNextAction()
        {
            if (_lootingBrain.IsBotLooting)
            {
                return new Action(typeof(LootingLogic), "Looting");
            }

            if (IsScheduledScan)
            {
                return new Action(typeof(FindLootLogic), "Loot Scan");
            }

            return new Action(typeof(PeacefulLogic), "Peaceful");
        }

        public override bool IsCurrentActionEnding()
        {
            Type currentActionType = CurrentAction?.Type;

            if (currentActionType == typeof(FindLootLogic))
            {
                bool lootScanDone = !_lootFinder.IsScanRunning;
                // Reset scan timer once scan is complete
                if (lootScanDone)
                {
                    ResetScanTimer();
                }

                return lootScanDone;
            }

            bool notLooting = !_lootingBrain.IsBotLooting;

            if (currentActionType == typeof(LootingLogic) && notLooting)
            {
                // Reset scan timer once looting has completed
                ResetScanTimer();
            }

            return notLooting;
        }

        void ResetScanTimer()
        {
            _scanTimer = Time.time + LootingBots.LootScanInterval.Value;
        }

        public override void BuildDebugText(StringBuilder debugPanel)
        {
            string itemName = _lootingBrain.ActiveItem?.Name?.Localized();
            string containerName = _lootingBrain.ActiveContainer?.name?.Localized();
            string corpseName = _lootingBrain.ActiveCorpse?.name?.Localized();
            string lootableName = itemName ?? containerName ?? corpseName ?? "-";

            string category = "";
            if (itemName != null)
            {
                category = "Item";
            }
            else if (containerName != null)
            {
                category = "Container";
            }
            else if (corpseName != null)
            {
                category = "Corpse";
            }

            debugPanel.AppendLine(
                _lootingBrain.LootTaskRunning ? "Looting in progress..." : "",
                Color.green
            );
            debugPanel.AppendLabeledValue(
                $"Target Loot",
                $" {lootableName} ({category})",
                Color.yellow,
                Color.yellow
            );

            debugPanel.AppendLabeledValue(
                $"Distance to Loot",
                $" {(category == "" || _lootingBrain.DistanceToLoot == -1f ? "Calculating path..." : $"{Math.Sqrt(_lootingBrain.DistanceToLoot):0.##}m")}",
                Color.grey,
                Color.grey
            );

            _lootingBrain.Stats.StatsDebugPanel(debugPanel);
        }

        public bool EndLooting()
        {
            return _lootingBrain.ActiveContainer == null
                && _lootingBrain.ActiveCorpse == null
                && _lootingBrain.ActiveItem == null;
        }
    }
}
