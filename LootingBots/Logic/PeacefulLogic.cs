using DrakiaXYZ.BigBrain.Brains;

using EFT;
//Check in CreateNode(BotLogicDecision type, BotOwner bot) (BotActionNodesClass on 4.0 to set this)
using PeacefulNodeClass = GClass266;

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
