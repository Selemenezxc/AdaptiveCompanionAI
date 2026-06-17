using AdaptiveCompanionAI.Common.Players;
using AdaptiveCompanionAI.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Common.Commands
{
    public class CompanionCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "adl";
        public override string Usage => "/adl spawn | ui | power <0.0-10.0> | duel <on|off>";
        public override string Description => "Управление адаптивным компаньоном";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            Player player = caller.Player;
            AdaptiveDifficultyPlayer data = player.GetModPlayer<AdaptiveDifficultyPlayer>();

            if (args.Length == 0)
            {
                caller.Reply("Использование: /adl spawn | ui | power <0.0-10.0> | duel <on|off>");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "spawn":
                    AdaptiveCompanionSystem.SpawnOrRecallCompanion(player, false);
                    caller.Reply("Команда компаньону отправлена.");
                    break;

                case "ui":
                    AdaptiveCompanionSystem.OpenNearestCompanionInterface(player);
                    caller.Reply("Окно компаньона открыто.");
                    break;

                case "power":
                    if (args.Length < 2 || !float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float power))
                    {
                        caller.Reply("Пример: /adl power 2.5");
                        return;
                    }

                    data.SetManualScalar(power, Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient);
                    caller.Reply($"Ручной коэффициент установлен: {data.ManualPowerScalar:0.00}");
                    break;

                case "duel":
                    if (!AdaptiveCompanionSystem.HasOwnedCompanion(player, out NPC npc))
                    {
                        caller.Reply("Компаньон не найден.");
                        return;
                    }

                    bool enabled = args.Length > 1 && args[1].Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    AdaptiveCompanionSystem.ToggleDuel(player, npc.whoAmI, enabled);
                    caller.Reply(enabled ? "Дуэль запущена." : "Дуэль остановлена.");
                    break;

                default:
                    caller.Reply("Неизвестная команда. Использование: /adl spawn | ui | power <0.0-10.0> | duel <on|off>");
                    break;
            }
        }
    }
}
