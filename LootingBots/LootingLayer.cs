using System;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Brain.Logics;
using LootingBots.Patch.Components;

using UnityEngine;

namespace LootingBots.Brain
{
    internal class LootingLayer : CustomLayer
    {
        private readonly LootFinder _lootFinder;
        private float _scanTimer;


        public LootingLayer(BotOwner botOwner, int priority)
            : base(botOwner, priority)
        {
            LootFinder lootFinder = botOwner.GetPlayer.gameObject.AddComponent<LootFinder>();
            lootFinder.Init(botOwner);
            _lootFinder = lootFinder;
        }

        public override string GetName()
        {
            return "Looting";
        }

        public override bool IsActive()
        {
            return (_scanTimer < Time.time && _lootFinder.WaitAfterLootTimer < Time.time) || _lootFinder.HasActiveLootable();
        }

        public override void Start()
        {
            _lootFinder.EnableTransactions();
            base.Start();
        }

        public override void Stop()
        {
            _lootFinder.DisableTransactions();
            base.Stop();
        }

        public override Action GetNextAction()
        {
            if (!_lootFinder.HasActiveLootable()) {
                _scanTimer = Time.time + 6f;
                return new Action(typeof(FindLootLogic), "Loot Scan");
            }

            if (_lootFinder.HasActiveLootable()) {
                return new Action(typeof(LootingLogic), "Looting");
            }

            return new Action(typeof(PeacefulLogic), "Peaceful");
        }

        public override bool IsCurrentActionEnding()
        {
            Type currentActionType = CurrentAction?.Type;
            return currentActionType != typeof(LootingLogic) || EndLooting();
        }

        public bool EndLooting()
        {
            return _lootFinder.ActiveContainer == null
                && _lootFinder.ActiveCorpse == null
                && _lootFinder.ActiveItem == null;
        }
    }
}
