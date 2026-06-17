using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class CompanionInventory
    {
        public CompanionInventory()
        {
            Storage = CreateArray(40);
            Armor = CreateArray(3);
            Accessories = CreateArray(7);
            HiddenArmor = new bool[3];
            HiddenAccessories = new bool[7];
        }

        public Item[] Storage { get; }
        public Item[] Armor { get; }
        public Item[] Accessories { get; }
        public bool[] HiddenArmor { get; }
        public bool[] HiddenAccessories { get; }

        public int StorageUsedSlots => Storage.Count(item => item != null && !item.IsAir);
        public int ArmorUsedSlots => Armor.Count(item => item != null && !item.IsAir);
        public int AccessoryUsedSlots => Accessories.Count(item => item != null && !item.IsAir);
        public int HiddenArmorSlots => HiddenArmor.Count(hidden => hidden);
        public int HiddenAccessorySlots => HiddenAccessories.Count(hidden => hidden);

        public int WeaponSlotsUsed => Storage.Count(IsCombatWeapon);
        public int AmmoSlotsUsed => Storage.Count(item => item != null && !item.IsAir && item.stack > 0 && item.ammo > 0);

        public IEnumerable<Item> EnumerateWeapons()
        {
            foreach (Item item in Storage)
            {
                if (IsCombatWeapon(item))
                {
                    yield return item;
                }
            }
        }

        public static bool IsCombatWeapon(Item item)
        {
            if (item == null || item.IsAir || item.damage <= 0)
            {
                return false;
            }

            if (item.ammo > 0 && item.useAmmo <= 0)
            {
                return false;
            }

            return item.useStyle > 0 || item.shoot > 0 || item.pick > 0 || item.axe > 0 || item.hammer > 0;
        }

        public void ToggleArmorVisibility(int index)
        {
            if (index >= 0 && index < HiddenArmor.Length)
            {
                HiddenArmor[index] = !HiddenArmor[index];
            }
        }

        public void ToggleAccessoryVisibility(int index)
        {
            if (index >= 0 && index < HiddenAccessories.Length)
            {
                HiddenAccessories[index] = !HiddenAccessories[index];
            }
        }

        public bool IsArmorHidden(int index)
        {
            return index >= 0 && index < HiddenArmor.Length && HiddenArmor[index];
        }

        public bool IsAccessoryHidden(int index)
        {
            return index >= 0 && index < HiddenAccessories.Length && HiddenAccessories[index];
        }

        public TagCompound SaveTag()
        {
            return new TagCompound
            {
                { "storage", SaveArray(Storage) },
                { "armor", SaveArray(Armor) },
                { "accessories", SaveArray(Accessories) },
                { "hiddenArmor", SaveBoolArray(HiddenArmor) },
                { "hiddenAccessories", SaveBoolArray(HiddenAccessories) },
            };
        }

        public void LoadTag(TagCompound tag)
        {
            LoadArray(Storage, tag.ContainsKey("storage") ? tag.GetList<TagCompound>("storage") : null);
            LoadArray(Armor, tag.ContainsKey("armor") ? tag.GetList<TagCompound>("armor") : null);
            LoadArray(Accessories, tag.ContainsKey("accessories") ? tag.GetList<TagCompound>("accessories") : null);
            LoadBoolArray(HiddenArmor, tag.ContainsKey("hiddenArmor") ? tag.GetList<int>("hiddenArmor") : null);
            LoadBoolArray(HiddenAccessories, tag.ContainsKey("hiddenAccessories") ? tag.GetList<int>("hiddenAccessories") : null);
        }

        private static Item[] CreateArray(int size)
        {
            Item[] items = new Item[size];
            for (int i = 0; i < size; i++)
            {
                items[i] = new Item();
                items[i].TurnToAir();
            }

            return items;
        }

        private static IList<TagCompound> SaveArray(Item[] items)
        {
            List<TagCompound> tags = new List<TagCompound>(items.Length);
            foreach (Item item in items)
            {
                tags.Add(ItemIO.Save(item));
            }

            return tags;
        }

        private static IList<int> SaveBoolArray(bool[] values)
        {
            List<int> saved = new List<int>(values.Length);
            foreach (bool value in values)
            {
                saved.Add(value ? 1 : 0);
            }

            return saved;
        }

        private static void LoadArray(Item[] destination, IList<TagCompound> saved)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = new Item();
                destination[i].TurnToAir();

                if (saved != null && i < saved.Count)
                {
                    destination[i] = ItemIO.Load(saved[i]);
                }
            }
        }

        private static void LoadBoolArray(bool[] destination, IList<int> saved)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = saved != null && i < saved.Count && saved[i] != 0;
            }
        }
    }
}
