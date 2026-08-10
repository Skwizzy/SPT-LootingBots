using EFT.InventoryLogic;

namespace LootingBots.Utilities;

[Flags]
public enum EquipmentType
{
    Backpack = 1,
    TacticalRig = 2,
    ArmoredRig = 4,
    Chest = 8,
    Weapon = 16,
    Grenade = 32,
    Helmet = 64,
    Dogtag = 128,
    ArmorPlate = 256,
    Earpiece = 512,
    FaceCover = 1024,
    Eyewear = 2048,
    Armband = 4096,

    All =
        Backpack
        | TacticalRig
        | ArmoredRig
        | Chest
        | Weapon
        | Helmet
        | Grenade
        | Dogtag
        | ArmorPlate
        | Earpiece
        | FaceCover
        | Eyewear
        | Armband,
}

[Flags]
public enum CanEquipEquipmentType
{
    Backpack = EquipmentType.Backpack,
    TacticalRig = EquipmentType.TacticalRig,
    ArmoredRig = EquipmentType.ArmoredRig,
    Chest = EquipmentType.Chest,
    Weapon = EquipmentType.Weapon,
    Grenade = EquipmentType.Grenade,
    Helmet = EquipmentType.Helmet,
    Earpiece = EquipmentType.Earpiece,
    FaceCover = EquipmentType.FaceCover,
    Eyewear = EquipmentType.Eyewear,
    Armband = EquipmentType.Armband,

    All = Backpack | TacticalRig | ArmoredRig | Chest | Weapon | Helmet | Grenade | Earpiece | FaceCover | Eyewear | Armband,
}

public static class EquipmentTypeUtils
{
    public static bool HasBackpack(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Backpack) != 0;
    }

    public static bool HasTacticalRig(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.TacticalRig) != 0;
    }

    public static bool HasArmoredRig(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.ArmoredRig) != 0;
    }

    public static bool HasChestArmor(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Chest) != 0;
    }

    public static bool HasGrenade(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Grenade) != 0;
    }

    public static bool HasWeapon(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Weapon) != 0;
    }

    public static bool HasHelmet(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Helmet) != 0;
    }

    public static bool HasArmorPlate(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.ArmorPlate) != 0;
    }

    public static bool HasDogtag(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Dogtag) != 0;
    }

    public static bool HasEarpiece(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Earpiece) != 0;
    }

    public static bool HasFaceCover(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.FaceCover) != 0;
    }

    public static bool HasEyewear(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Eyewear) != 0;
    }

    public static bool HasArmband(this EquipmentType equipmentType)
    {
        return (equipmentType & EquipmentType.Armband) != 0;
    }

    /// <summary>
    /// GClasses based off GClass3373.FindSlotToPickUp
    /// </summary>
    public static bool IsItemEligible(this EquipmentType allowedGear, Item item, bool toPickup = false)
    {
        if (IsChestArmor(item))
        {
            return allowedGear.HasChestArmor();
        }

        if (IsHelmet(item))
        {
            return allowedGear.HasHelmet();
        }

        if (IsBackpack(item))
        {
            return allowedGear.HasBackpack();
        }

        if (IsEarpiece(item))
        {
            return allowedGear.HasEarpiece();
        }

        if (IsFaceCover(item))
        {
            return allowedGear.HasFaceCover();
        }

        if (IsEyewear(item))
        {
            return allowedGear.HasEyewear();
        }

        if (IsArmoredRig(item))
        {
            return allowedGear.HasArmoredRig();
        }

        if (IsTacticalRig(item))
        {
            return allowedGear.HasTacticalRig();
        }

        if (IsArmorPlate(item))
        {
            return allowedGear.HasArmorPlate();
        }

        if (IsDogtag(item))
        {
            return allowedGear.HasDogtag();
        }

        if (item is EFT.InventoryLogic.Knife) { }

        if (item is EFT.InventoryLogic.ThrowWeap)
        {
            return allowedGear.HasGrenade();
        }

        if (item is Weapon)
        {
            return allowedGear.HasWeapon();
        }

        if (IsArmband(item))
        {
            return allowedGear.HasArmband();
        }

        return toPickup;
    }

    public static bool IsTacticalRig(Item item)
    {
        return item is EFT.InventoryLogic.Vest;
    }

    public static bool IsArmoredRig(Item item)
    {
        if (item is EFT.InventoryLogic.Vest vest)
        {
            foreach (var slot in vest.Slots)
            {
                // If any slot is an armor slot
                if (slot is ArmorSlot)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsBackpack(Item item)
    {
        return item is EFT.InventoryLogic.Backpack;
    }

    public static bool IsHelmet(Item item)
    {
        return item is EFT.InventoryLogic.Headwear;
    }

    public static bool IsChestArmor(Item item)
    {
        return item is EFT.InventoryLogic.Armor;
    }

    public static bool IsFaceCover(Item item)
    {
        return item is EFT.InventoryLogic.FaceCover;
    }

    public static bool IsEyewear(Item item)
    {
        return item is EFT.InventoryLogic.Visors;
    }

    public static bool IsArmorPlate(Item item)
    {
        return item is EFT.InventoryLogic.ArmorPlate;
    }

    public static bool IsDogtag(Item item)
    {
        return item is EFT.InventoryLogic.BarterOther;
    }

    public static bool IsEarpiece(Item item)
    {
        return item is EFT.InventoryLogic.Headphones;
    }

    public static bool IsArmband(Item item)
    {
        return item is EFT.InventoryLogic.ArmBand;
    }
}
