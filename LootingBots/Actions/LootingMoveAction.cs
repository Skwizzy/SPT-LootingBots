using EFT.InventoryLogic;
using LootingBots.Components;
using LootingBots.Utilities;
using UnityEngine.Pool;

namespace LootingBots.Actions;

/// <summary>
/// Move action to be executed
/// </summary>
public class LootingMoveAction : LootingAction
{
    private static readonly UnityEngine.Pool.ObjectPool<LootingMoveAction> _pool = new(
        Create,
        null,
        a => a.Reset(),
        ListActionPool.LogOnDestroyInstance,
        true,
        2,
        32
    );

    public static LootingMoveAction Create()
    {
        return new LootingMoveAction();
    }

    public static LootingMoveAction Rent(Item item, ItemAddress place = null, float netWorthDelta = 0f)
    {
        var moveAction = _pool.Get();
        moveAction.Item = item;
        moveAction.Place = place;
        moveAction.NetWorthDelta = netWorthDelta;

        return moveAction;
    }

    /// <summary>
    /// Move item to this address, if null try to equip the item
    /// </summary>
    public ItemAddress Place { get; set; }

    public override Task<bool> ExecuteAsync(LootingTransactionController controller, CancellationToken token)
    {
        return controller.MoveItemAsync(Item, Place, token);
    }

    public override void Return()
    {
        _pool.Release(this);
    }

    protected override void Reset()
    {
        base.Reset();
        Place = null;
    }
}
