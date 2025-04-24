using DrakiaXYZ.BigBrain.Brains;

using EFT;

//Check in CreateNode(BotLogicDecision type, BotOwner bot) (GClass212 on 3.9 to set this)
using PeacefulNodeClass = GClass212;

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