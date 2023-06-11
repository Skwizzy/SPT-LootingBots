using DrakiaXYZ.BigBrain.Brains;
using EFT;

namespace LootingBots.Brain.Logics
{
    internal class PeacefulLogic : CustomLogic
    {
        private readonly GClass176 _baseLogic;

        // PatrolAssault peacful logic
        public PeacefulLogic(BotOwner botOwner) : base(botOwner)
        {
            _baseLogic = new GClass176(botOwner);
        }

        public override void Update()
        {
            _baseLogic.Update();
        }
    }
}