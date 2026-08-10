using EFT.InventoryLogic;

namespace LootingBots.Utilities;

public static class NonLinqUtils
{
    // SPT 4.1.2 note: these two helpers existed to avoid the per-frame LINQ allocation in
    // BSG's own implementations, iterating the active-event list (List_0) by hand. In 4.1.2
    // List_0 became the private field _activeEvents, exposed as the ActiveEvents property,
    // and BSG now ships both checks natively as ItemController.IsChangingWeapon and
    // ItemController.HasAnyHandsAction. Delegating to those is correct and drops the
    // GEventArgs9/GEventArgs10/GInterface418 dependencies, which could not be identified in
    // the renamed 4.1.2 type graph (single-member and zero-member types carry no signal).
    //
    // If profiling shows these allocating on the hot path, reinstate the manual loop over
    // controller.ActiveEvents once the event-arg types have been identified.

    public static bool IsChangingWeaponNonLinq(this InventoryController controller)
    {
        return controller.IsChangingWeapon;
    }

    public static bool HasAnyHandsActionNonLinq(this ItemController controller)
    {
        return controller.HasAnyHandsAction();
    }
}
