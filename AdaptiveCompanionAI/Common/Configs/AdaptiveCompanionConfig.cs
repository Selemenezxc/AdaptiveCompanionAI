using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace AdaptiveCompanionAI.Common.Configs
{
    public class AdaptiveCompanionConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [DefaultValue(true)]
        public bool AutoSpawnOnEnterWorld { get; set; }

        [DefaultValue(0.75f)]
        [Range(0.25f, 2f)]
        public float MinimumAutoCoefficient { get; set; }

        [DefaultValue(2.25f)]
        [Range(1f, 4f)]
        public float MaximumAutoCoefficient { get; set; }

        [DefaultValue(5)]
        [Range(1, 30)]
        public int RespawnDelaySeconds { get; set; }

        [DefaultValue(600)]
        [Range(200, 2000)]
        public int EngagementRadius { get; set; }
    }
}
