using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.UI;

namespace AdaptiveCompanionAI.Common.UI
{
    /// <summary>
    /// Isolated item slot for the companion UI.
    ///
    /// The vanilla EquipArmor/EquipAccessory contexts are intentionally not used for input handling:
    /// those contexts operate on the local player's equipment and can duplicate or move items into
    /// the player's own armor slots. This class draws vanilla-looking slots, but performs all item
    /// movement against the companion arrays only.
    /// </summary>
    public class CompanionItemSlot : UIElement
    {
        private readonly Item[] _items;
        private readonly int _slotIndex;
        private readonly int _drawContext;
        private readonly Func<Item, bool> _canAcceptItem;
        private readonly string _rejectHint;
        private readonly Action _onChanged;

        public CompanionItemSlot(Item[] items, int slotIndex, int drawContext, Func<Item, bool> canAcceptItem = null, string rejectHint = null, Action onChanged = null)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _slotIndex = slotIndex;
            _drawContext = drawContext;
            _canAcceptItem = canAcceptItem;
            _rejectHint = rejectHint ?? "Этот предмет нельзя положить в выбранный слот.";
            _onChanged = onChanged;

            Width.Set(52f, 0f);
            Height.Set(52f, 0f);
        }

        public static bool IsHeadArmor(Item item)
        {
            return item != null && !item.IsAir && item.headSlot >= 0;
        }

        public static bool IsBodyArmor(Item item)
        {
            return item != null && !item.IsAir && item.bodySlot >= 0;
        }

        public static bool IsLegArmor(Item item)
        {
            return item != null && !item.IsAir && item.legSlot >= 0;
        }

        public static bool IsAccessory(Item item)
        {
            return item != null && !item.IsAir && item.accessory;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (evt.Target != this || !IsValidSlot())
            {
                return;
            }

            Item mouseItem = Main.mouseItem;
            Item slotItem = _items[_slotIndex];

            if ((mouseItem == null || mouseItem.IsAir) && IsAir(slotItem))
            {
                return;
            }

            if (!IsAir(mouseItem) && !CanAccept(mouseItem))
            {
                Main.NewText(_rejectHint, 255, 190, 120);
                return;
            }

            if (IsAir(mouseItem))
            {
                Main.mouseItem = CloneOrAir(slotItem);
                _items[_slotIndex].TurnToAir();
                MarkChanged(true);
                return;
            }

            if (IsAir(slotItem))
            {
                _items[_slotIndex] = CloneOrAir(mouseItem);
                Main.mouseItem.TurnToAir();
                MarkChanged(true);
                return;
            }

            if (TryMergeStacks(slotItem, Main.mouseItem))
            {
                MarkChanged(true);
                return;
            }

            Item swap = CloneOrAir(slotItem);
            _items[_slotIndex] = CloneOrAir(Main.mouseItem);
            Main.mouseItem = swap;
            MarkChanged(true);
        }

        public override void RightClick(UIMouseEvent evt)
        {
            base.RightClick(evt);

            if (evt.Target != this || !IsValidSlot())
            {
                return;
            }

            Item mouseItem = Main.mouseItem;
            Item slotItem = _items[_slotIndex];

            if (!IsAir(mouseItem) && !CanAccept(mouseItem))
            {
                Main.NewText(_rejectHint, 255, 190, 120);
                return;
            }

            if (IsAir(mouseItem) && !IsAir(slotItem))
            {
                Main.mouseItem = CloneSingle(slotItem);
                RemoveOneFromSlot();
                MarkChanged(true);
                return;
            }

            if (!IsAir(mouseItem) && IsAir(slotItem))
            {
                _items[_slotIndex] = CloneSingle(mouseItem);
                RemoveOneFromMouse();
                MarkChanged(true);
                return;
            }

            if (!IsAir(mouseItem) && !IsAir(slotItem) && CanStackTogether(slotItem, mouseItem) && slotItem.stack < slotItem.maxStack)
            {
                slotItem.stack++;
                RemoveOneFromMouse();
                MarkChanged(true);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            Vector2 position = dimensions.Position();

            ItemSlot.Draw(spriteBatch, _items, _drawContext, _slotIndex, position);

            if (!ContainsPoint(Main.MouseScreen))
            {
                return;
            }

            Main.LocalPlayer.mouseInterface = true;
            ItemSlot.MouseHover(_items, _drawContext, _slotIndex);

            Item mouseItem = Main.mouseItem;
            if (!IsAir(mouseItem) && !CanAccept(mouseItem))
            {
                Main.hoverItemName = _rejectHint;
            }
        }

        private bool IsValidSlot()
        {
            return _slotIndex >= 0 && _slotIndex < _items.Length;
        }

        private bool CanAccept(Item item)
        {
            return IsAir(item) || _canAcceptItem == null || _canAcceptItem(item);
        }

        private static bool IsAir(Item item)
        {
            return item == null || item.IsAir;
        }

        private static Item CloneOrAir(Item item)
        {
            if (IsAir(item))
            {
                return CreateAirItem();
            }

            return item.Clone();
        }

        private static Item CloneSingle(Item item)
        {
            Item clone = CloneOrAir(item);
            if (!clone.IsAir)
            {
                clone.stack = 1;
            }

            return clone;
        }

        private static Item CreateAirItem()
        {
            Item item = new Item();
            item.TurnToAir();
            return item;
        }

        private static bool CanStackTogether(Item destination, Item source)
        {
            return !IsAir(destination)
                && !IsAir(source)
                && destination.type == source.type
                && destination.prefix == source.prefix
                && destination.maxStack > 1
                && source.maxStack > 1
                && destination.stack < destination.maxStack;
        }

        private static bool TryMergeStacks(Item destination, Item source)
        {
            if (!CanStackTogether(destination, source))
            {
                return false;
            }

            int moveCount = Math.Min(source.stack, destination.maxStack - destination.stack);
            if (moveCount <= 0)
            {
                return false;
            }

            destination.stack += moveCount;
            source.stack -= moveCount;
            if (source.stack <= 0)
            {
                source.TurnToAir();
            }

            return true;
        }

        private void RemoveOneFromSlot()
        {
            Item slotItem = _items[_slotIndex];
            if (IsAir(slotItem))
            {
                return;
            }

            slotItem.stack--;
            if (slotItem.stack <= 0)
            {
                slotItem.TurnToAir();
            }
        }

        private static void RemoveOneFromMouse()
        {
            if (IsAir(Main.mouseItem))
            {
                return;
            }

            Main.mouseItem.stack--;
            if (Main.mouseItem.stack <= 0)
            {
                Main.mouseItem.TurnToAir();
            }
        }

        private void MarkChanged(bool playSound)
        {
            if (playSound)
            {
                SoundEngine.PlaySound(SoundID.Grab);
            }

            ItemSlot.RefreshStackSplitCooldown();
            _onChanged?.Invoke();
        }
    }
}
