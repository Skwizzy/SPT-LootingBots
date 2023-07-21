using System;

using EFT.InventoryLogic;

using BodyArmorClass = GClass2536;
using HeadArmorClass = GClass2535;
using BackpackItemClass = GClass2582;
using TacticalRigItemClass = GClass2583;

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

        // GClasses based off GClass2642.FindSlotToPickUp
        public static bool IsItemEligible(this EquipmentType allowedGear, Item item)
        {
            if (item is BodyArmorClass)
            {
                return allowedGear.HasArmorVest();
            }
        
            if (item is HeadArmorClass headwear && headwear.IsArmorMod())
            {
                return allowedGear.HasHelmet();
            }

            if (item is BackpackItemClass)
            {
                return allowedGear.HasBackpack();
            }
            if (item is TacticalRigItemClass tacRig)
            {
                return tacRig.IsArmorMod()
                    ? allowedGear.HasArmoredRig()
                    : allowedGear.HasTacticalRig();
            }

            if (item is KnifeClass) { }

            if (item is GrenadeClass) {
                return allowedGear.HasGrenade();
            }

            if (item is Weapon)
            {
                return allowedGear.HasWeapon();
            }

            return true;
        }

    }
}
