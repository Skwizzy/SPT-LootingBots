using EFT.InventoryLogic;
using LootingBots.Components;
using LootingBots.Utilities;
using UnityEngine.Pool;

namespace LootingBots.Actions;

/// <summary>
/// Swap action to be executed
/// </summary>
/// <inheritdoc/>
public class LootingSwapAction : LootingAction
{
    private static readonly UnityEngine.Pool.ObjectPool<LootingSwapAction> _pool = new(
        Create,
        null,
        a => a.Reset(),
        ListActionPool.LogOnDestroyInstance,
        true,
        2,
        32
    );

    public static LootingSwapAction Create()
    {
        return new LootingSwapAction();
    }

    public static LootingSwapAction Rent(Item item, Item toSwap, float netWorthDelta = 0f, bool transferItems = false)
    {
        var swapAction = _pool.Get();
        swapAction.Item = item;
        swapAction.ToSwap = toSwap;
        swapAction.NetWorthDelta = netWorthDelta;
        swapAction.TransferItems = transferItems;

        return swapAction;
    }

    /// <summary>
    /// Item to be swapped with
    /// </summary>
    public Item ToSwap { get; set; }

    /// <summary>
    /// Loot items from thrown item if true
    /// </summary>
    public bool TransferItems { get; set; }

    public override async Task<bool> ExecuteAsync(LootingTransactionController controller, CancellationToken token)
    {
        if (await controller.SwapItemsAsync(Item, ToSwap, token))
        {
            return true;
        }

        // Swap failed, try throwing first then equipping after.
        // Check if item can be equipped to ToSwap's address,
        // then rollback since we're not simulating
        var toSwapAddress = ToSwap.CurrentAddress;
        var inventoryController = ToSwap.Owner as InventoryController;
        var removeResult = EFT.InventoryLogic.ItemManipulator.Remove(ToSwap, inventoryController, false);
        var moveResult = EFT.InventoryLogic.ItemManipulator.Move(Item, toSwapAddress, inventoryController, false);

        moveResult.Value?.RollBack();
        removeResult.Value?.RollBack();

        if (moveResult.Failed)
        {
            return false;
        }

        // If throw-equip simulation was successful, run it
        return await controller.ThrowItemAsync(ToSwap, token) && await controller.TryEquipItemAsync(Item, token);
    }

    public override void Return()
    {
        _pool.Release(this);
    }

    protected override void Reset()
    {
        base.Reset();
        ToSwap = null;
        TransferItems = false;
    }
}
