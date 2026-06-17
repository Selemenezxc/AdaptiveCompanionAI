using System.IO;
using AdaptiveCompanionAI.Common.Data;
using AdaptiveCompanionAI.Common.Enums;
using AdaptiveCompanionAI.Common.Players;
using AdaptiveCompanionAI.Common.Systems;
using AdaptiveCompanionAI.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI
{
    public class AdaptiveCompanionAI : Mod
    {
        public static AdaptiveCompanionAI Instance { get; private set; }
        public static ModKeybind OpenCompanionUIHotKey { get; private set; }

        public override void Load()
        {
            Instance = this;

            if (!Main.dedServ)
            {
                OpenCompanionUIHotKey = KeybindLoader.RegisterKeybind(this, "Open Companion UI", "O");
            }
        }

        public override void Unload()
        {
            Instance = null;
            OpenCompanionUIHotKey = null;
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            CompanionNetMessage messageType = (CompanionNetMessage)reader.ReadByte();

            switch (messageType)
            {
                case CompanionNetMessage.RequestSpawn:
                {
                    byte playerIndex = reader.ReadByte();
                    if (Main.netMode == NetmodeID.Server && playerIndex < Main.maxPlayers)
                    {
                        Player player = Main.player[playerIndex];
                        if (player.active)
                        {
                            AdaptiveCompanionSystem.SpawnOrRecallCompanion(player, true);
                        }
                    }
                    break;
                }
                case CompanionNetMessage.SetManualScalar:
                {
                    byte playerIndex = reader.ReadByte();
                    float scalar = reader.ReadSingle();
                    if (playerIndex < Main.maxPlayers)
                    {
                        Player player = Main.player[playerIndex];
                        if (player.active)
                        {
                            player.GetModPlayer<AdaptiveDifficultyPlayer>().SetManualScalar(scalar, false);
                        }
                    }
                    break;
                }
                case CompanionNetMessage.ToggleDuel:
                {
                    int npcIndex = reader.ReadInt32();
                    bool enabled = reader.ReadBoolean();
                    if (npcIndex >= 0 && npcIndex < Main.maxNPCs)
                    {
                        NPC npc = Main.npc[npcIndex];
                        if (npc.active && npc.ModNPC is AdaptiveCompanionNPC companion)
                        {
                            if (Main.netMode == NetmodeID.Server && companion.OwnerPlayerIndex != whoAmI)
                            {
                                break;
                            }

                            companion.SetDuelState(enabled);
                            npc.netUpdate = true;
                        }
                    }
                    break;
                }
                case CompanionNetMessage.SetSkillSimulation:
                {
                    byte playerIndex = reader.ReadByte();
                    CompanionSkillSimulationMode mode = (CompanionSkillSimulationMode)reader.ReadByte();
                    if (playerIndex < Main.maxPlayers)
                    {
                        Player player = Main.player[playerIndex];
                        if (player.active)
                        {
                            player.GetModPlayer<AdaptiveDifficultyPlayer>().SetSkillSimulationMode(mode, false);
                        }
                    }
                    break;
                }
                case CompanionNetMessage.SetManualStyle:
                {
                    byte playerIndex = reader.ReadByte();
                    bool manual = reader.ReadBoolean();
                    CompanionCombatStyle style = (CompanionCombatStyle)reader.ReadByte();
                    if (playerIndex < Main.maxPlayers)
                    {
                        Player player = Main.player[playerIndex];
                        if (player.active)
                        {
                            player.GetModPlayer<AdaptiveDifficultyPlayer>().SetManualCombatStyle(manual, style, false);
                        }
                    }
                    break;
                }
                case CompanionNetMessage.SetManualProfile:
                {
                    byte playerIndex = reader.ReadByte();
                    bool manual = reader.ReadBoolean();
                    CompanionPowerProfile profile = (CompanionPowerProfile)reader.ReadByte();
                    if (playerIndex < Main.maxPlayers)
                    {
                        Player player = Main.player[playerIndex];
                        if (player.active)
                        {
                            player.GetModPlayer<AdaptiveDifficultyPlayer>().SetManualPowerProfile(manual, profile, false);
                        }
                    }
                    break;
                }
            }
        }

        public static void SendSpawnRequest(byte playerIndex)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.RequestSpawn);
            packet.Write(playerIndex);
            packet.Send();
        }

        public static void SendManualScalarPacket(byte playerIndex, float scalar)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.SetManualScalar);
            packet.Write(playerIndex);
            packet.Write(scalar);
            packet.Send();
        }

        public static void SendToggleDuelPacket(int npcIndex, bool enabled)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.ToggleDuel);
            packet.Write(npcIndex);
            packet.Write(enabled);
            packet.Send();
        }

        public static void SendSkillSimulationPacket(byte playerIndex, CompanionSkillSimulationMode mode)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.SetSkillSimulation);
            packet.Write(playerIndex);
            packet.Write((byte)mode);
            packet.Send();
        }

        public static void SendManualStylePacket(byte playerIndex, bool manual, CompanionCombatStyle style)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.SetManualStyle);
            packet.Write(playerIndex);
            packet.Write(manual);
            packet.Write((byte)style);
            packet.Send();
        }

        public static void SendManualProfilePacket(byte playerIndex, bool manual, CompanionPowerProfile profile)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || Instance == null)
            {
                return;
            }

            ModPacket packet = Instance.GetPacket();
            packet.Write((byte)CompanionNetMessage.SetManualProfile);
            packet.Write(playerIndex);
            packet.Write(manual);
            packet.Write((byte)profile);
            packet.Send();
        }
    }
}
