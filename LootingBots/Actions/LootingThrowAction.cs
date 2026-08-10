using EFT.InventoryLogic;
using LootingBots.Components;
using LootingBots.Utilities;
using UnityEngine.Pool;

namespace LootingBots.Actions;

/// <summary>
/// Throw action to be executed
/// </summary>
public class LootingThrowAction : LootingAction
{
    private static readonly UnityEngine.Pool.ObjectPool<LootingThrowAction> _pool = new(
        Create,
        null,
        a => a.Reset(),
        ListActionPool.LogOnDestroyInstance,
        true,
        32
    );

    public static LootingThrowAction Create()
    {
        return new LootingThrowAction();
    }

    public static LootingThrowAction Rent(Item item, float netWorthDelta = 0f, bool transferItems = true)
    {
        var throwAction = _pool.Get();
        throwAction.Item = item;
        throwAction.NetWorthDelta = netWorthDelta;
        throwAction.TransferItems = transferItems;

        return throwAction;
    }

    /// <summary>
    /// Loot items from thrown item if true
    /// </summary>
    public bool TransferItems { get; set; }

    public override Task<bool> ExecuteAsync(LootingTransactionController controller, CancellationToken token)
    {
        return controller.ThrowItemAsync(Item, token);
    }

    public override void Return()
    {
        _pool.Release(this);
    }

    protected override void Reset()
    {
        base.Reset();
        TransferItems = false;
    }
}
