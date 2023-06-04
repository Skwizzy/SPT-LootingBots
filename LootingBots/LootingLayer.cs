using System;

using DrakiaXYZ.BigBrain.Brains;

using EFT;

using LootingBots.Brain.Logics;
using LootingBots.Patch.Components;

namespace LootingBots.Brain
{
    internal class LootingLayer : CustomLayer
    {
        private readonly LootFinder _lootFinder;

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
            return _lootFinder.HasActiveLootable();
        }

        public override void Start()
        {
            _lootFinder.Resume();
            base.Start();
        }

        public override void Stop()
        {
            _lootFinder.Pause();
            base.Stop();
        }

        public override Action GetNextAction()
        {
            return new Action(typeof(LootingLogic), "Looting");
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
