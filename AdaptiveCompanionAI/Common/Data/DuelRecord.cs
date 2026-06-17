using Terraria.ModLoader.IO;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class DuelRecord
    {
        public int DurationSeconds { get; set; }
        public int PlayerDamageDealt { get; set; }
        public int CompanionDamageDealt { get; set; }
        public string Winner { get; set; } = "-";

        public TagCompound SaveTag()
        {
            return new TagCompound
            {
                { "durationSeconds", DurationSeconds },
                { "playerDamageDealt", PlayerDamageDealt },
                { "companionDamageDealt", CompanionDamageDealt },
                { "winner", Winner ?? "-" },
            };
        }

        public static DuelRecord FromTag(TagCompound tag)
        {
            return new DuelRecord
            {
                DurationSeconds = tag.ContainsKey("durationSeconds") ? tag.GetInt("durationSeconds") : 0,
                PlayerDamageDealt = tag.ContainsKey("playerDamageDealt") ? tag.GetInt("playerDamageDealt") : 0,
                CompanionDamageDealt = tag.ContainsKey("companionDamageDealt") ? tag.GetInt("companionDamageDealt") : 0,
                Winner = tag.ContainsKey("winner") ? tag.GetString("winner") : "-",
            };
        }
    }
}
