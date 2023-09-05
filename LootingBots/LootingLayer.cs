using System;
using System.Text;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Brain.Logics;
using LootingBots.Patch.Components;

using UnityEngine;

namespace LootingBots.Brain
{
    internal class LootingLayer : CustomLayer
    {
        private readonly LootingBrain _lootingBrain;
        private float _scanTimer;

        private bool IsScheduledScan
        {
            get { return _scanTimer < Time.time && _lootingBrain.WaitAfterLootTimer < Time.time; }
        }

        public LootingLayer(BotOwner botOwner, int priority)
            : base(botOwner, priority)
        {
            LootingBrain lootingBrain = botOwner.GetPlayer.gameObject.AddComponent<LootingBrain>();
            lootingBrain.Init(botOwner);
            _scanTimer = Time.time + LootingBots.InitialStartTimer.Value;
            _lootingBrain = lootingBrain;
        }

        public override string GetName()
        {
            return "Looting";
        }

        public override bool IsActive()
        {
            bool isBotActive = BotOwner.BotState == EBotState.Active;
            return isBotActive && (IsScheduledScan || _lootingBrain.IsBotLooting);
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
                _scanTimer = Time.time + 6f;
                return new Action(typeof(FindLootLogic), "Loot Scan");
            }

            return new Action(typeof(PeacefulLogic), "Peaceful");
        }

        public override bool IsCurrentActionEnding()
        {
            Type currentActionType = CurrentAction?.Type;

            if (currentActionType == typeof(FindLootLogic))
            {
                return _lootingBrain.HasActiveLootable;
            }

            return !_lootingBrain.IsBotLooting;
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
