using AdaptiveCompanionAI.Common.Configs;
using AdaptiveCompanionAI.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class CompanionPowerSnapshot
    {
        public float AutoCoefficient { get; set; }
        public float ManualCoefficient { get; set; }
        public float EffectiveCoefficient { get; set; }
        public float DuelCoefficient { get; set; }
        public float PlayerSkillScore { get; set; }
        public float MeasuredPlayerSkillScore { get; set; }
        public float AssistanceNeed { get; set; }
        public float CombatScore { get; set; }
        public float SurvivalScore { get; set; }
        public float MobilityScore { get; set; }
        public float ExplorationScore { get; set; }
        public float GearScore { get; set; }
        public float InputScore { get; set; }
        public string PreferredStyle { get; set; }
        public int PreferredStyleCode { get; set; }
        public string PowerProfile { get; set; }
        public string SkillSource { get; set; }
        public string StyleSource { get; set; }
        public string ProfileSource { get; set; }
        public PlayerProgressSnapshot Progress { get; set; }
    }

    public static class CompanionPowerModel
    {
        public static CompanionPowerSnapshot Evaluate(Player player, AdaptiveDifficultyPlayer ownerData)
        {
            AdaptiveCompanionConfig config = ModContent.GetInstance<AdaptiveCompanionConfig>();
            PlayerProgressSnapshot progress = PlayerProgressSnapshot.Capture(player);
            MetricSnapshot metrics = ownerData.PlayerMetrics;

            double playTime = System.Math.Max(1d, metrics.Get("play_time_seconds"));
            double playMinutes = playTime / 60d;
            double playHours = playTime / 3600d;
            double damageDealt = metrics.Get("damage_dealt_total");
            double damageTaken = metrics.Get("damage_taken_total");
            double deaths = metrics.Get("death_count");
            double kills = metrics.Get("npc_kills_total") + metrics.Get("boss_kills_total");
            double bossFocus = CompanionMath.SafeRate(metrics.Get("boss_damage_total"), damageDealt + 1d);
            double damageDealtPerMinute = CompanionMath.SafeRate(damageDealt, playMinutes + 0.05d);
            double damageTakenPerMinute = CompanionMath.SafeRate(damageTaken, playMinutes + 0.05d);
            double killsPerMinute = CompanionMath.SafeRate(kills, playMinutes + 0.05d);
            double deathsPerHour = CompanionMath.SafeRate(deaths, playHours + 0.05d);
            double potionRate = CompanionMath.SafeRate(metrics.Get("potion_uses_total"), playMinutes + 0.05d);
            double inputRate = CompanionMath.SafeRate(metrics.Get("active_key_load_total"), playTime + 1d);
            double transitionRate = CompanionMath.SafeRate(metrics.Get("key_transition_total"), playTime + 1d);
            double movementRate = CompanionMath.SafeRate(metrics.Get("movement_distance_total"), playTime + 1d);
            double biomeCoverage = CalculateBiomeCoverage(metrics, playTime);

            double combatScore = CompanionMath.Clamp01(
                damageDealtPerMinute / 1100d * 0.42d
                + killsPerMinute / 2.5d * 0.23d
                + bossFocus * 0.18d
                + CompanionMath.SafeRate(metrics.Get("crit_hits_total"), metrics.Get("projectile_hits_total") + metrics.Get("melee_hits_total") + 1d) * 0.17d);

            double survivalPenalty = CompanionMath.Clamp01(damageTakenPerMinute / 900d * 0.55d + deathsPerHour / 14d * 0.35d + potionRate / 5d * 0.1d);
            double survivalScore = CompanionMath.Clamp01(
                (progress.LifeMax / 500d) * 0.28d
                + (progress.Defense / 95d) * 0.26d
                + (1d - survivalPenalty) * 0.46d);

            double mobilityScore = CompanionMath.Clamp01(
                movementRate / 1800d * 0.36d
                + metrics.Get("max_speed_observed") / 15d * 0.24d
                + CompanionMath.SafeRate(metrics.Get("jump_count"), playMinutes + 0.05d) / 80d * 0.12d
                + CompanionMath.SafeRate(metrics.Get("flight_time_seconds"), playTime + 1d) * 0.28d);

            double inputScore = CompanionMath.Clamp01(inputRate / 7.5d * 0.45d + transitionRate / 9d * 0.35d + CompanionMath.SafeRate(metrics.Get("reaction_change_total"), playTime + 1d) / 2d * 0.20d);
            double explorationScore = CompanionMath.Clamp01(biomeCoverage * 0.48d + progress.ProgressRatio * 0.52d);
            double gearScore = CompanionMath.Clamp01(progress.ArmorRating / 85d * 0.45d + progress.CurrentWeaponDamage / 140d * 0.35d + progress.InventoryFillRatio * 0.2d);

            double measuredSkillScore = CompanionMath.Clamp01(
                combatScore * 0.26d
                + survivalScore * 0.28d
                + mobilityScore * 0.16d
                + explorationScore * 0.12d
                + gearScore * 0.12d
                + inputScore * 0.06d);

            double skillScore = ResolveSkillSimulation(ownerData.SkillSimulationMode, measuredSkillScore);
            string skillSource = GetSkillSimulationLabel(ownerData.SkillSimulationMode);

            double assistanceNeed = CompanionMath.Clamp01(1d - skillScore + CompanionMath.Clamp01(damageTakenPerMinute / 850d) * 0.18d + CompanionMath.Clamp01(deathsPerHour / 18d) * 0.18d - combatScore * 0.10d);

            float minCoefficient = System.Math.Min(config.MinimumAutoCoefficient, config.MaximumAutoCoefficient);
            float maxCoefficient = System.Math.Max(config.MinimumAutoCoefficient, config.MaximumAutoCoefficient);

            double autoCoefficient = 0.90d + progress.ProgressRatio * 0.42d + assistanceNeed * 2.25d + CompanionMath.Clamp01(damageTakenPerMinute / 700d) * 0.35d;
            autoCoefficient -= combatScore * 0.16d;

            string profileSource = "Авто";
            string powerProfile;
            if (ownerData.ManualPowerProfileEnabled)
            {
                autoCoefficient = ResolveManualProfileCoefficient(ownerData.ManualPowerProfile, progress.ProgressRatio, assistanceNeed);
                powerProfile = GetPowerProfileLabel(ownerData.ManualPowerProfile);
                profileSource = "Ручной";
            }
            else
            {
                powerProfile = ResolvePowerProfile((float)autoCoefficient);
            }

            autoCoefficient = CompanionMath.Clamp(autoCoefficient, minCoefficient, maxCoefficient);

            double duelCoefficient = 0.50d + skillScore * 1.85d + progress.ProgressRatio * 0.28d;
            if (ownerData.SkillSimulationMode == CompanionSkillSimulationMode.Low)
            {
                duelCoefficient *= 0.72d;
            }
            else if (ownerData.SkillSimulationMode == CompanionSkillSimulationMode.High)
            {
                duelCoefficient *= 1.12d;
            }

            duelCoefficient = CompanionMath.Clamp(duelCoefficient, 0.35d, 3.15d);

            ResolveStyle(metrics, out string style, out int styleCode);
            string styleSource = "Авто";
            if (ownerData.ManualCombatStyleEnabled)
            {
                styleCode = (int)ownerData.ManualCombatStyle;
                style = GetCombatStyleLabel(ownerData.ManualCombatStyle);
                styleSource = "Ручной";
            }

            float effectiveCoefficient = (float)(autoCoefficient * ownerData.ManualPowerScalar);
            float effectiveDuelCoefficient = (float)(duelCoefficient * ownerData.ManualPowerScalar);
            if (!ownerData.ManualPowerProfileEnabled)
            {
                powerProfile = ResolvePowerProfile(effectiveCoefficient);
            }

            return new CompanionPowerSnapshot
            {
                AutoCoefficient = (float)autoCoefficient,
                ManualCoefficient = ownerData.ManualPowerScalar,
                EffectiveCoefficient = effectiveCoefficient,
                DuelCoefficient = effectiveDuelCoefficient,
                PlayerSkillScore = (float)skillScore,
                MeasuredPlayerSkillScore = (float)measuredSkillScore,
                AssistanceNeed = (float)assistanceNeed,
                CombatScore = (float)combatScore,
                SurvivalScore = (float)survivalScore,
                MobilityScore = (float)mobilityScore,
                ExplorationScore = (float)explorationScore,
                GearScore = (float)gearScore,
                InputScore = (float)inputScore,
                PreferredStyle = style,
                PreferredStyleCode = styleCode,
                PowerProfile = powerProfile,
                SkillSource = skillSource,
                StyleSource = styleSource,
                ProfileSource = profileSource,
                Progress = progress,
            };
        }

        public static double CalculateBiomeCoverage(MetricSnapshot metrics, double playTime)
        {
            double biomeTime = metrics.Get("jungle_time_seconds")
                + metrics.Get("desert_time_seconds")
                + metrics.Get("snow_time_seconds")
                + metrics.Get("hallow_time_seconds")
                + metrics.Get("corruption_crimson_time_seconds")
                + metrics.Get("dungeon_time_seconds")
                + metrics.Get("underworld_time_seconds")
                + metrics.Get("underwater_time_seconds");

            return CompanionMath.Clamp01(biomeTime / (playTime + 1d));
        }

        public static string GetSkillSimulationLabel(CompanionSkillSimulationMode mode)
        {
            return mode switch
            {
                CompanionSkillSimulationMode.Low => "Низкий",
                CompanionSkillSimulationMode.Medium => "Средний",
                CompanionSkillSimulationMode.High => "Высокий",
                _ => "Настоящий",
            };
        }

        public static string GetCombatStyleLabel(CompanionCombatStyle style)
        {
            return style switch
            {
                CompanionCombatStyle.Melee => "Ближний бой",
                CompanionCombatStyle.Ranged => "Дальний бой",
                CompanionCombatStyle.Magic => "Магия",
                CompanionCombatStyle.Summon => "Призыв",
                _ => "Сбалансированный",
            };
        }

        public static string GetPowerProfileLabel(CompanionPowerProfile profile)
        {
            return profile switch
            {
                CompanionPowerProfile.Weak => "Ослабленный",
                CompanionPowerProfile.Support => "Поддержка",
                CompanionPowerProfile.Elite => "Элитный",
                _ => "Сбалансированный",
            };
        }

        private static double ResolveSkillSimulation(CompanionSkillSimulationMode mode, double measuredSkillScore)
        {
            return mode switch
            {
                CompanionSkillSimulationMode.Low => 0.24d,
                CompanionSkillSimulationMode.Medium => 0.55d,
                CompanionSkillSimulationMode.High => 0.86d,
                _ => measuredSkillScore,
            };
        }

        private static double ResolveManualProfileCoefficient(CompanionPowerProfile profile, double progressRatio, double assistanceNeed)
        {
            return profile switch
            {
                CompanionPowerProfile.Weak => 0.70d + progressRatio * 0.10d,
                CompanionPowerProfile.Support => 1.35d + assistanceNeed * 0.65d + progressRatio * 0.15d,
                CompanionPowerProfile.Elite => 2.05d + assistanceNeed * 0.75d + progressRatio * 0.25d,
                _ => 0.98d + assistanceNeed * 0.45d + progressRatio * 0.20d,
            };
        }

        private static void ResolveStyle(MetricSnapshot metrics, out string style, out int styleCode)
        {
            double melee = metrics.Get("melee_hits_total");
            double ranged = metrics.Get("ranged_hits_total");
            double magic = metrics.Get("magic_hits_total");
            double summon = metrics.Get("summon_hits_total");

            style = "Сбалансированный";
            styleCode = (int)CompanionCombatStyle.Balanced;
            double best = melee;

            if (ranged > best)
            {
                best = ranged;
                style = "Дальний бой";
                styleCode = (int)CompanionCombatStyle.Ranged;
            }

            if (magic > best)
            {
                best = magic;
                style = "Магия";
                styleCode = (int)CompanionCombatStyle.Magic;
            }

            if (summon > best)
            {
                best = summon;
                style = "Призыв";
                styleCode = (int)CompanionCombatStyle.Summon;
            }

            if (best == melee && best > 0d)
            {
                style = "Ближний бой";
                styleCode = (int)CompanionCombatStyle.Melee;
            }
        }

        private static string ResolvePowerProfile(float effectiveCoefficient)
        {
            if (effectiveCoefficient <= 0.01f)
            {
                return "Отключен";
            }

            if (effectiveCoefficient < 0.75f)
            {
                return "Ослабленный";
            }

            if (effectiveCoefficient < 1.5f)
            {
                return "Сбалансированный";
            }

            if (effectiveCoefficient < 3f)
            {
                return "Усиленный";
            }

            if (effectiveCoefficient < 6f)
            {
                return "Элитный";
            }

            return "Запредельный";
        }
    }
}
