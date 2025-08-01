using EFT.InventoryLogic;

namespace LootingBots.Actions
{
    public class LootingSwapAction(
        Item toThrow = null,
        Item toEquip = null,
        ActionCallback callback = null,
        ActionCallback onComplete = null
    )
    {
        public Item ToThrow { get; private set; } = toThrow;
        public Item ToEquip { get; private set; } = toEquip;
        public ActionCallback Callback { get; private set; } = callback;
        public ActionCallback OnComplete { get; private set; } = onComplete;
    }
}
