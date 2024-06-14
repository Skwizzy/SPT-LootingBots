using System;

using EFT.InventoryLogic;

using FaceCoveringClass = GClass2635;
using ArmorPlateClass = GClass2633;
using HeadArmorClass = GClass2636;
using BodyArmorClass = GClass2637;
using BackpackItemClass = GClass2684;
using TacticalRigItemClass = GClass2685;
using DogtagClass = GClass2713;

namespace LootingBots.Patch.Util
{
    [Flags]
    public enum EquipmentType
    {
        Backpack = 1,
        TacticalRig = 2,
        ArmoredRig = 4,
        ArmorVest = 8,
        Weapon = 16,
        Grenade = 32,
        Helmet = 64,
        Dogtag = 128,
        ArmorPlate = 256,

        All =
            Backpack
            | TacticalRig
            | ArmoredRig
            | ArmorVest
            | Weapon
            | Helmet
            | Grenade
            | Dogtag
            | ArmorPlate
    }

    [Flags]
    public enum CanEquipEquipmentType
    {
        Backpack = EquipmentType.Backpack,
        TacticalRig = EquipmentType.TacticalRig,
        ArmoredRig = EquipmentType.ArmoredRig,
        ArmorVest = EquipmentType.ArmorVest,
        Weapon = EquipmentType.Weapon,
        Grenade = EquipmentType.Grenade,
        Helmet = EquipmentType.Helmet,

        All = Backpack | TacticalRig | ArmoredRig | ArmorVest | Weapon | Helmet | Grenade
    }

    public static class EquipmentTypeUtils
    {
        public static bool HasBackpack(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.Backpack);
        }

        public static bool HasTacticalRig(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.TacticalRig);
        }

        public static bool HasArmoredRig(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.ArmoredRig);
        }

        public static bool HasArmorVest(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.ArmorVest);
        }

        public static bool HasGrenade(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.Grenade);
        }

        public static bool HasWeapon(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.Weapon);
        }

        public static bool HasHelmet(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.Helmet);
        }

        public static bool HasArmorPlate(this EquipmentType equipmentType)
        {
            return equipmentType.HasFlag(EquipmentType.ArmorPlate);
        }

        public static bool HasDogtag(this EquipmentType equipmentType) {
            return equipmentType.HasFlag(EquipmentType.Dogtag);
        }

        // GClasses based off GClass2558.FindSlotToPickUp
        public static bool IsItemEligible(this EquipmentType allowedGear, Item item)
        {
            if (IsArmorVest(item))
            {
                return allowedGear.HasArmorVest();
            }

            if (IsHelmet(item))
            {
                return allowedGear.HasHelmet();
            }

            if (IsBackpack(item))
            {
                return allowedGear.HasBackpack();
            }

            if (IsArmoredRig(item))
            {
                return allowedGear.HasArmoredRig();
            }

            if (IsTacticalRig(item))
            {
                return allowedGear.HasTacticalRig();
            }

            if (IsArmorPlate(item, out ArmorPlateClass _))
            {
                return allowedGear.HasArmorPlate();
            }

            if (IsDogtag(item)) {
                return allowedGear.HasDogtag();
            }

            if (item is KnifeClass) { }

            if (item is GrenadeClass)
            {
                return allowedGear.HasGrenade();
            }

            if (item is Weapon)
            {
                return allowedGear.HasWeapon();
            }

            return true;
        }

        public static bool IsTacticalRig(Item item)
        {
            return item is TacticalRigItemClass;
        }

        public static bool IsArmoredRig(Item item)
        {
            return item is TacticalRigItemClass && item.IsArmorMod();
        }

        public static bool IsBackpack(Item item)
        {
            return item is BackpackItemClass;
        }

        public static bool IsHelmet(Item item)
        {
            return item is HeadArmorClass;
        }

        public static bool IsArmorVest(Item item)
        {
            return item is BodyArmorClass;
        }

        public static bool IsFaceCovering(Item item)
        {
            return item is FaceCoveringClass;
        }

        public static bool IsArmorPlate(Item item, out ArmorPlateClass plate)
        {
            bool isArmorPlate = item is ArmorPlateClass;
            plate = isArmorPlate ? (ArmorPlateClass)item : null;

            return isArmorPlate;
        }

        public static bool IsDogtag(Item item) {
            return item is DogtagClass;
        }
    }
}
