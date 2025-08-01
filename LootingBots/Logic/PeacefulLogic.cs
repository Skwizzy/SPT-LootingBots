using DrakiaXYZ.BigBrain.Brains;

using EFT;
//Check in CreateNode(BotLogicDecision type, BotOwner bot) (GClass522 on 3.11 to set this)
using PeacefulNodeClass = GClass259;

namespace LootingBots.Logic
{
    internal class PeacefulLogic : CustomLogic
    {
        private readonly PeacefulNodeClass _baseLogic;

        // PatrolAssault peaceful logic
        public PeacefulLogic(BotOwner botOwner)
            : base(botOwner)
        {
            _baseLogic = new PeacefulNodeClass(botOwner);
        }

        public override void Update(CustomLayer.ActionData data)
        {
            _baseLogic.UpdateNodeByBrain(data);
        }
    }
}
