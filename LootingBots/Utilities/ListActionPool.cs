using LootingBots.Actions;
using UnityEngine.Pool;

namespace LootingBots.Utilities;

public static class ListActionPool
{
    private static readonly UnityEngine.Pool.ObjectPool<List<LootingAction>> _pool = new(Create, null, OnRelease, LogOnDestroyInstance, true, 2, 32);

    public static List<LootingAction> Create()
    {
        return [];
    }

    /// <summary>
    /// Rent an instance from the pool
    /// </summary>
    public static List<LootingAction> Rent()
    {
        return _pool.Get();
    }

    /// <summary>
    /// Return an instance to the pool
    /// </summary>
    public static void Return(List<LootingAction> list)
    {
        _pool.Release(list);
    }

    /// <summary>
    /// Resets all the elements in the list and clears the list.
    /// Used when reusing the list before returning.
    /// </summary>
    public static void Reset(List<LootingAction> list)
    {
        OnRelease(list);
    }

    /// <summary>
    /// Return each LootingAction on the list and then clear the list
    /// </summary>
    private static void OnRelease(List<LootingAction> lootingAction)
    {
        foreach (var action in lootingAction)
        {
            action.Return();
        }
        lootingAction.Clear();
    }

    public static void LogOnDestroyInstance<T>(T instance)
    {
        var log = LootingBots.LootLog;
        if (log.DebugEnabled)
        {
            LootingBots.LootLog.LogError($"Destroyed instance of {instance.GetType().FullName}");
        }
    }
}
