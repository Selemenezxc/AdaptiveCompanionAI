using System.Collections.Generic;
using AdaptiveCompanionAI.Common.UI;
using AdaptiveCompanionAI.Content.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace AdaptiveCompanionAI.Common.Systems
{
    public class AdaptiveCompanionSystem : ModSystem
    {
        public const float FullInterfaceDistance = 190f;
        internal static UserInterface CompanionInterface;
        internal static CompanionUIState CompanionUI;
        internal static int BoundCompanionNpcIndex = -1;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                CompanionInterface = new UserInterface();
                CompanionUI = new CompanionUIState();
                CompanionUI.Activate();
            }
        }

        public override void Unload()
        {
            CompanionInterface = null;
            CompanionUI = null;
            BoundCompanionNpcIndex = -1;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (CompanionInterface?.CurrentState != null)
            {
                CompanionInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "AdaptiveCompanionAI: Inventory Companion Button",
                    delegate
                    {
                        DrawInventoryAccessButton(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI));

                layers.Insert(inventoryIndex + 2, new LegacyGameInterfaceLayer(
                    "AdaptiveCompanionAI: Companion Interface",
                    delegate
                    {
                        if (CompanionInterface?.CurrentState != null)
                        {
                            CompanionInterface.Draw(Main.spriteBatch, new GameTime());
                        }

                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        public static bool IsInterfaceVisible => CompanionInterface?.CurrentState != null;

        public static bool IsInterfaceOpenFor(int npcIndex)
        {
            return IsInterfaceVisible && BoundCompanionNpcIndex == npcIndex;
        }

        public static void OpenNearestCompanionInterface(Player player)
        {
            if (HasOwnedCompanion(player, out NPC companion))
            {
                OpenInterface(companion.whoAmI);
                return;
            }

            int npcIndex = SpawnOrRecallCompanion(player, false);
            if (npcIndex >= 0)
            {
                OpenInterface(npcIndex);
            }
        }

        public static void OpenInterface(int npcIndex)
        {
            if (Main.dedServ || CompanionUI == null || CompanionInterface == null)
            {
                return;
            }

            Main.playerInventory = true;
            BoundCompanionNpcIndex = npcIndex;
            CompanionUI.Bind(npcIndex);
            CompanionInterface.SetState(CompanionUI);
        }

        public static void CloseInterface()
        {
            if (Main.dedServ || CompanionInterface == null)
            {
                return;
            }

            BoundCompanionNpcIndex = -1;
            CompanionInterface.SetState(null);
        }

        public static bool HasOwnedCompanion(Player player, out NPC npc)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC activeNpc = Main.npc[i];
                if (!activeNpc.active || activeNpc.type != ModContent.NPCType<AdaptiveCompanionNPC>())
                {
                    continue;
                }

                if (activeNpc.ModNPC is AdaptiveCompanionNPC companion && companion.OwnerPlayerIndex == player.whoAmI)
                {
                    npc = activeNpc;
                    return true;
                }
            }

            npc = null;
            return false;
        }

        public static bool IsCompanionClose(Player player, NPC npc)
        {
            return player != null && npc != null && npc.active && Vector2.Distance(player.Center, npc.Center) <= FullInterfaceDistance;
        }

        public static int SpawnOrRecallCompanion(Player player, bool forceSpawn)
        {
            if (HasOwnedCompanion(player, out NPC existing))
            {
                existing.Center = player.Center + new Vector2(player.direction == 0 ? 48f : player.direction * 48f, -36f);
                existing.velocity = Vector2.Zero;
                existing.netUpdate = true;
                return existing.whoAmI;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient && !forceSpawn)
            {
                AdaptiveCompanionAI.SendSpawnRequest((byte)player.whoAmI);
                return -1;
            }

            int index = NPC.NewNPC(new EntitySource_Misc("AdaptiveCompanionSpawn"), (int)player.Center.X, (int)player.Center.Y, ModContent.NPCType<AdaptiveCompanionNPC>(), 0, player.whoAmI);
            if (index >= 0 && index < Main.maxNPCs && Main.npc[index].ModNPC is AdaptiveCompanionNPC companionNpc)
            {
                companionNpc.BindToOwner(player.whoAmI);
            }

            return index;
        }

        public static void ToggleDuel(Player player, int npcIndex, bool enabled)
        {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            {
                return;
            }

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.ModNPC is not AdaptiveCompanionNPC companion || companion.OwnerPlayerIndex != player.whoAmI)
            {
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                AdaptiveCompanionAI.SendToggleDuelPacket(npcIndex, enabled);
                return;
            }

            if (enabled)
            {
                player.statLife = player.statLifeMax2;
                npc.life = npc.lifeMax;
            }

            companion.SetDuelState(enabled);
            npc.netUpdate = true;
        }

        private static void DrawInventoryAccessButton(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || !Main.playerInventory || CompanionInterface?.CurrentState != null)
            {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active)
            {
                return;
            }

            bool hasCompanion = HasOwnedCompanion(player, out NPC companion);
            bool close = hasCompanion && IsCompanionClose(player, companion);
            Rectangle button = GetInventoryButtonRectangle();
            bool hovered = button.Contains(Main.MouseScreen.ToPoint());
            Color border = close ? new Color(122, 157, 255) : new Color(145, 145, 155);
            Color background = close ? new Color(63, 82, 151, 220) : new Color(62, 66, 82, 210);

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, button, background);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(button.X, button.Y, button.Width, 1), border);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(button.X, button.Bottom - 1, button.Width, 1), border);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(button.X, button.Y, 1, button.Height), border);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(button.Right - 1, button.Y, 1, button.Height), border);

            string text = close ? "AI" : "R";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
            Utils.DrawBorderString(spriteBatch, text, new Vector2(button.Center.X, button.Center.Y) - textSize * 0.5f, Color.White, 0.82f);

            if (!hovered)
            {
                return;
            }

            player.mouseInterface = true;
            Main.hoverItemName = close ? "Открыть интерфейс компаньона" : hasCompanion ? "Компаньон далеко — открыть возврат" : "Призвать компаньона";

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                SoundEngine.PlaySound(SoundID.MenuTick);

                if (!hasCompanion)
                {
                    int created = SpawnOrRecallCompanion(player, false);
                    if (created >= 0)
                    {
                        OpenInterface(created);
                    }
                    return;
                }

                // If the companion is far away, open the restricted interface first.
                // The actual recall action lives in the Inventory tab so unavailable controls stay visibly disabled.
                OpenInterface(companion.whoAmI);
            }
        }

        private static Rectangle GetInventoryButtonRectangle()
        {
            int x = 558 + Main.trashSlotOffset.X;
            int y = 260 + Main.trashSlotOffset.Y;

            if (x < 20 || x > Main.screenWidth - 42)
            {
                x = Main.screenWidth - 92;
            }

            if (y < 20 || y > Main.screenHeight - 42)
            {
                y = 258;
            }

            return new Rectangle(x, y, 34, 34);
        }
    }
}
