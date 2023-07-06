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

        public LootingLayer(BotOwner botOwner, int priority)
            : base(botOwner, priority)
        {
            LootingBrain lootingBrain = botOwner.GetPlayer.gameObject.AddComponent<LootingBrain>();
            lootingBrain.Init(botOwner);
            _lootingBrain = lootingBrain;
        }

        public override string GetName()
        {
            return "Looting";
        }

        public override bool IsActive()
        {
            bool scheduledScan =
                _scanTimer < Time.time && _lootingBrain.WaitAfterLootTimer < Time.time;

            bool isBotActive = BotOwner.BotState == EBotState.Active;
            return isBotActive && (scheduledScan || _lootingBrain.HasActiveLootable());
        }

        public override void Start()
        {
            _lootingBrain.EnableTransactions();
            base.Start();
        }

        public override void Stop()
        {
            _lootingBrain.DisableTransactions();
            base.Stop();
        }

        public override Action GetNextAction()
        {
            if (!_lootingBrain.HasActiveLootable())
            {
                _scanTimer = Time.time + 6f;
                return new Action(typeof(FindLootLogic), "Loot Scan");
            }

            if (_lootingBrain.HasActiveLootable())
            {
                return new Action(typeof(LootingLogic), "Looting");
            }

            return new Action(typeof(PeacefulLogic), "Peaceful");
        }

        public override bool IsCurrentActionEnding()
        {
            Type currentActionType = CurrentAction?.Type;
            return currentActionType != typeof(LootingLogic) || EndLooting();
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
                _lootingBrain.IsLooting ? "Looting in progress..." : "",
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
                $" {(category == "" ? '-' : _lootingBrain.DistanceToLoot)}",
                Color.grey,
                Color.grey
            );
            
            _lootingBrain.ItemAdder.Stats.StatsDebugPanel(debugPanel);
        }

        public bool EndLooting()
        {
            return _lootingBrain.ActiveContainer == null
                && _lootingBrain.ActiveCorpse == null
                && _lootingBrain.ActiveItem == null;
        }
    }
}
