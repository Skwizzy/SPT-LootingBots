using DrakiaXYZ.BigBrain.Brains;
using EFT;

using PeacefulNodeClass = GClass177;

namespace LootingBots.Brain.Logics
{
    internal class PeacefulLogic : CustomLogic
    {
        private readonly PeacefulNodeClass _baseLogic;

        // PatrolAssault peaceful logic
        public PeacefulLogic(BotOwner botOwner) : base(botOwner)
        {
            _baseLogic = new PeacefulNodeClass(botOwner);
        }

        public override void Update()
        {
            _baseLogic.Update();
        }
    }
}