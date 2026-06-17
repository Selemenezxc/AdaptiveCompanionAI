using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace AdaptiveCompanionAI.Common.UI
{
    public class UIDragPanel : UIPanel
    {
        private Vector2 _offset;
        private bool _dragging;

        public UIDragPanel()
        {
            BackgroundColor = new Color(33, 43, 79) * 0.92f;
            BorderColor = new Color(89, 116, 213) * 0.85f;
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);

            // Do not start dragging when the user interacts with item slots or buttons.
            // Static text has IgnoresMouseInteraction enabled, so the header area remains draggable.
            if (evt.Target == this)
            {
                StartDrag(evt);
            }
        }

        public override void LeftMouseUp(UIMouseEvent evt)
        {
            base.LeftMouseUp(evt);

            if (_dragging)
            {
                EndDrag(evt);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_dragging)
            {
                Left.Set(Main.mouseX - _offset.X, 0f);
                Top.Set(Main.mouseY - _offset.Y, 0f);
                Recalculate();
            }

            Rectangle parentSpace = GetDimensions().ToRectangle();
            if (parentSpace.X < 0)
            {
                Left.Set(0f, 0f);
                Recalculate();
            }

            if (parentSpace.Y < 0)
            {
                Top.Set(0f, 0f);
                Recalculate();
            }

            if (parentSpace.Right > Main.screenWidth)
            {
                Left.Set(Main.screenWidth - Width.Pixels, 0f);
                Recalculate();
            }

            if (parentSpace.Bottom > Main.screenHeight)
            {
                Top.Set(Main.screenHeight - Height.Pixels, 0f);
                Recalculate();
            }
        }

        private void StartDrag(UIMouseEvent evt)
        {
            _offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
            _dragging = true;
        }

        private void EndDrag(UIMouseEvent evt)
        {
            Vector2 end = evt.MousePosition;
            _dragging = false;
            Left.Set(end.X - _offset.X, 0f);
            Top.Set(end.Y - _offset.Y, 0f);
            Recalculate();
        }
    }
}
