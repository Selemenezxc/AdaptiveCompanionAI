using System.Collections.Generic;
using AdaptiveCompanionAI.Common.Configs;
using AdaptiveCompanionAI.Common.Data;
using AdaptiveCompanionAI.Common.Systems;
using AdaptiveCompanionAI.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AdaptiveCompanionAI.Common.Players
{
    public class AdaptiveDifficultyPlayer : ModPlayer
    {
        private const int LeftBit = 1 << 0;
        private const int RightBit = 1 << 1;
        private const int UpBit = 1 << 2;
        private const int DownBit = 1 << 3;
        private const int JumpBit = 1 << 4;
        private const int UseItemBit = 1 << 5;
        private const int UseTileBit = 1 << 6;
        private const int HookBit = 1 << 7;
        private const int QuickHealBit = 1 << 8;
        private const int QuickManaBit = 1 << 9;
        private const int MaxDuelHistoryRecords = 40;

        private Vector2 _lastPosition;
        private bool _positionInitialized;
        private int _lastLife;
        private int _lastMana;
        private int _lastChestIndex = -1;
        private int _lastPotionDelay;
        private int _lastControlMask;
        private int _lastHorizontalIntent;
        private double _speedAccumulator;
        private int _speedSamples;
        private int _spawnRetryCooldown;
        private double _followDistanceAccumulator;
        private int _followDistanceSamples;
        private bool _wasLowHealth;

        public MetricSnapshot PlayerMetrics { get; private set; }
        public MetricSnapshot CompanionMetrics { get; private set; }
        public CompanionInventory CompanionInventory { get; private set; }
        public CompanionPowerSnapshot CurrentPowerSnapshot { get; private set; }
        public List<DuelRecord> DuelHistory { get; private set; }
        public float ManualPowerScalar { get; private set; } = 1f;
        public CompanionSkillSimulationMode SkillSimulationMode { get; private set; } = CompanionSkillSimulationMode.Real;
        public bool ManualCombatStyleEnabled { get; private set; }
        public CompanionCombatStyle ManualCombatStyle { get; private set; } = CompanionCombatStyle.Balanced;
        public bool ManualPowerProfileEnabled { get; private set; }
        public CompanionPowerProfile ManualPowerProfile { get; private set; } = CompanionPowerProfile.Balanced;
        public string CurrentCompanionStyle { get; private set; } = "Сбалансированный";
        public string CurrentPowerProfile { get; private set; } = "Сбалансированный";

        public override void Initialize()
        {
            PlayerMetrics = new MetricSnapshot(MetricCatalog.Player);
            CompanionMetrics = new MetricSnapshot(MetricCatalog.Companion);
            CompanionInventory = new CompanionInventory();
            DuelHistory = new List<DuelRecord>(MaxDuelHistoryRecords);
            CurrentPowerSnapshot = new CompanionPowerSnapshot
            {
                AutoCoefficient = 1f,
                ManualCoefficient = 1f,
                EffectiveCoefficient = 1f,
                PreferredStyle = "Сбалансированный",
                PreferredStyleCode = 0,
                PowerProfile = "Сбалансированный",
                Progress = PlayerProgressSnapshot.Capture(Player),
            };

            ManualPowerScalar = 1f;
            SkillSimulationMode = CompanionSkillSimulationMode.Real;
            ManualCombatStyleEnabled = false;
            ManualCombatStyle = CompanionCombatStyle.Balanced;
            ManualPowerProfileEnabled = false;
            ManualPowerProfile = CompanionPowerProfile.Balanced;
            ResetRuntimeTracking();
        }

        public override void OnEnterWorld()
        {
            ResetRuntimeTracking();
            _spawnRetryCooldown = GetRespawnDelayTicks();
        }

        public override void PreUpdate()
        {
            EnsureInitialized();

            if (!_positionInitialized)
            {
                _lastPosition = Player.position;
                _positionInitialized = true;
            }

            PlayerMetrics.Increment("play_time_seconds", CompanionMath.SecondsPerTick);
            if (!Player.dead)
            {
                PlayerMetrics.Increment("alive_time_seconds", CompanionMath.SecondsPerTick);
            }

            UpdateEnvironmentMetrics();
            UpdateResourceMetrics();
            UpdateChestMetrics();
            UpdateCurrentSnapshotMetrics();
            UpdatePowerSnapshot();
            TryAutoSpawnCompanion();
        }

        public override void SetControls()
        {
            EnsureInitialized();

            int mask = 0;
            if (Player.controlLeft) mask |= LeftBit;
            if (Player.controlRight) mask |= RightBit;
            if (Player.controlUp) mask |= UpBit;
            if (Player.controlDown) mask |= DownBit;
            if (Player.controlJump) mask |= JumpBit;
            if (Player.controlUseItem) mask |= UseItemBit;
            if (Player.controlUseTile) mask |= UseTileBit;
            if (Player.controlHook) mask |= HookBit;
            if (Player.controlQuickHeal) mask |= QuickHealBit;
            if (Player.controlQuickMana) mask |= QuickManaBit;

            int activeKeys = CompanionMath.CountBits(mask);
            int transitions = CompanionMath.CountBits(mask ^ _lastControlMask);
            PlayerMetrics.Increment("active_key_load_total", activeKeys);
            PlayerMetrics.Increment("key_transition_total", transitions);

            if ((mask & JumpBit) != 0 && (_lastControlMask & JumpBit) == 0)
            {
                PlayerMetrics.Increment("jump_count");
            }

            if ((mask & UseItemBit) != 0 && (_lastControlMask & UseItemBit) == 0)
            {
                PlayerMetrics.Increment("item_use_presses_total");
            }

            if ((mask & QuickHealBit) != 0 && (_lastControlMask & QuickHealBit) == 0)
            {
                PlayerMetrics.Increment("quick_heal_key_presses_total");
            }

            if ((mask & QuickManaBit) != 0 && (_lastControlMask & QuickManaBit) == 0)
            {
                PlayerMetrics.Increment("quick_mana_key_presses_total");
            }

            int horizontalIntent = 0;
            if ((mask & LeftBit) != 0)
            {
                horizontalIntent = -1;
            }
            else if ((mask & RightBit) != 0)
            {
                horizontalIntent = 1;
            }

            if (horizontalIntent != 0 && _lastHorizontalIntent != 0 && horizontalIntent != _lastHorizontalIntent)
            {
                PlayerMetrics.Increment("reaction_change_total");
            }

            if (horizontalIntent != 0)
            {
                _lastHorizontalIntent = horizontalIntent;
            }

            _lastControlMask = mask;
        }

        public override void PreUpdateMovement()
        {
            EnsureInitialized();

            Vector2 delta = Player.position - _lastPosition;
            PlayerMetrics.Increment("movement_distance_total", delta.Length());
            PlayerMetrics.Increment("horizontal_distance_total", System.Math.Abs(delta.X));
            PlayerMetrics.Increment("vertical_distance_total", System.Math.Abs(delta.Y));

            double speed = Player.velocity.Length();
            _speedAccumulator += speed;
            _speedSamples++;

            if (speed > PlayerMetrics.Get("max_speed_observed"))
            {
                PlayerMetrics.Set("max_speed_observed", speed);
            }

            if (Player.mount != null && Player.mount.Active)
            {
                PlayerMetrics.Increment("mount_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.velocity.Y < -0.1f && Player.wingTime > 0)
            {
                PlayerMetrics.Increment("flight_time_seconds", CompanionMath.SecondsPerTick);
            }

            _lastPosition = Player.position;
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet)
        {
            if (AdaptiveCompanionAI.OpenCompanionUIHotKey != null && AdaptiveCompanionAI.OpenCompanionUIHotKey.JustPressed)
            {
                AdaptiveCompanionSystem.OpenNearestCompanionInterface(Player);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            PlayerMetrics.Increment("damage_dealt_total", damageDone);
            if (hit.Crit)
            {
                PlayerMetrics.Increment("crit_hits_total");
            }

            if (target.boss)
            {
                PlayerMetrics.Increment("boss_damage_total", damageDone);
                if (target.life <= 0)
                {
                    PlayerMetrics.Increment("boss_kills_total");
                }
            }
            else if (target.life <= 0)
            {
                PlayerMetrics.Increment("npc_kills_total");
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            PlayerMetrics.Increment("melee_hits_total");
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (CompanionProjectileGlobal.IsFromCompanion(proj))
            {
                return;
            }

            PlayerMetrics.Increment("projectile_hits_total");

            if (proj.DamageType == DamageClass.Ranged)
            {
                PlayerMetrics.Increment("ranged_hits_total");
            }
            else if (proj.DamageType == DamageClass.Magic)
            {
                PlayerMetrics.Increment("magic_hits_total");
            }
            else if (proj.DamageType == DamageClass.Summon || proj.DamageType == DamageClass.SummonMeleeSpeed)
            {
                PlayerMetrics.Increment("summon_hits_total");
            }
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            PlayerMetrics.Increment("damage_taken_total", info.Damage);
        }

        public override void OnConsumeMana(Item item, int manaConsumed)
        {
            PlayerMetrics.Increment("mana_spent_total", manaConsumed);
            if (item != null && item.DamageType == DamageClass.Magic)
            {
                PlayerMetrics.Increment("magic_hits_total", 0.2d);
            }
        }

        public override bool OnPickup(Item item)
        {
            PlayerMetrics.Increment("item_pickups_total");
            PlayerMetrics.Increment("coin_value_picked_total", CompanionMath.GetCoinValue(item));
            return true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            PlayerMetrics.Increment("death_count");
            return true;
        }

        public override void OnRespawn()
        {
            PlayerMetrics.Increment("respawn_count");
            _spawnRetryCooldown = GetRespawnDelayTicks();
        }

        public override void SaveData(Terraria.ModLoader.IO.TagCompound tag)
        {
            tag.Add("playerMetrics", PlayerMetrics.SaveTag());
            tag.Add("companionMetrics", CompanionMetrics.SaveTag());
            tag.Add("companionInventory", CompanionInventory.SaveTag());
            tag.Add("manualPowerScalar", ManualPowerScalar);
            tag.Add("skillSimulationMode", (int)SkillSimulationMode);
            tag.Add("manualCombatStyleEnabled", ManualCombatStyleEnabled);
            tag.Add("manualCombatStyle", (int)ManualCombatStyle);
            tag.Add("manualPowerProfileEnabled", ManualPowerProfileEnabled);
            tag.Add("manualPowerProfile", (int)ManualPowerProfile);
            tag.Add("duelHistory", SaveDuelHistory());
        }

        public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
        {
            EnsureInitialized();

            if (tag.ContainsKey("playerMetrics"))
            {
                PlayerMetrics.LoadTag(tag.GetCompound("playerMetrics"));
            }

            if (tag.ContainsKey("companionMetrics"))
            {
                CompanionMetrics.LoadTag(tag.GetCompound("companionMetrics"));
            }

            if (tag.ContainsKey("companionInventory"))
            {
                CompanionInventory.LoadTag(tag.GetCompound("companionInventory"));
            }

            if (tag.ContainsKey("manualPowerScalar"))
            {
                ManualPowerScalar = NormalizeManualScalar(tag.GetFloat("manualPowerScalar"));
            }
            else
            {
                ManualPowerScalar = 1f;
            }

            SkillSimulationMode = tag.ContainsKey("skillSimulationMode") ? NormalizeSkillMode(tag.GetInt("skillSimulationMode")) : CompanionSkillSimulationMode.Real;
            ManualCombatStyleEnabled = tag.ContainsKey("manualCombatStyleEnabled") && tag.GetBool("manualCombatStyleEnabled");
            ManualCombatStyle = tag.ContainsKey("manualCombatStyle") ? NormalizeCombatStyle(tag.GetInt("manualCombatStyle")) : CompanionCombatStyle.Balanced;
            ManualPowerProfileEnabled = tag.ContainsKey("manualPowerProfileEnabled") && tag.GetBool("manualPowerProfileEnabled");
            ManualPowerProfile = tag.ContainsKey("manualPowerProfile") ? NormalizePowerProfile(tag.GetInt("manualPowerProfile")) : CompanionPowerProfile.Balanced;
            LoadDuelHistory(tag.ContainsKey("duelHistory") ? tag.GetList<Terraria.ModLoader.IO.TagCompound>("duelHistory") : null);
        }

        public void SetManualScalar(float value, bool syncToServer)
        {
            ManualPowerScalar = NormalizeManualScalar(value);
            PlayerMetrics.Set("difficulty_coefficient_manual", ManualPowerScalar);
            CompanionMetrics.Set("manual_coefficient_current", ManualPowerScalar);

            if (syncToServer)
            {
                AdaptiveCompanionAI.SendManualScalarPacket((byte)Player.whoAmI, ManualPowerScalar);
            }
        }

        public void AdjustManualScalar(float delta, bool syncToServer)
        {
            SetManualScalar(ManualPowerScalar + delta, syncToServer);
        }

        public void SetSkillSimulationMode(CompanionSkillSimulationMode mode, bool syncToServer)
        {
            SkillSimulationMode = NormalizeSkillMode((int)mode);
            PlayerMetrics.Set("skill_simulation_mode", (int)SkillSimulationMode);

            if (syncToServer)
            {
                AdaptiveCompanionAI.SendSkillSimulationPacket((byte)Player.whoAmI, SkillSimulationMode);
            }
        }

        public void SetManualCombatStyle(bool manual, CompanionCombatStyle style, bool syncToServer)
        {
            ManualCombatStyleEnabled = manual;
            ManualCombatStyle = NormalizeCombatStyle((int)style);
            PlayerMetrics.Set("manual_style_enabled", ManualCombatStyleEnabled ? 1d : 0d);
            PlayerMetrics.Set("manual_style_code", (int)ManualCombatStyle);

            if (syncToServer)
            {
                AdaptiveCompanionAI.SendManualStylePacket((byte)Player.whoAmI, ManualCombatStyleEnabled, ManualCombatStyle);
            }
        }

        public void SetManualPowerProfile(bool manual, CompanionPowerProfile profile, bool syncToServer)
        {
            ManualPowerProfileEnabled = manual;
            ManualPowerProfile = NormalizePowerProfile((int)profile);
            PlayerMetrics.Set("manual_profile_enabled", ManualPowerProfileEnabled ? 1d : 0d);
            PlayerMetrics.Set("manual_profile_code", (int)ManualPowerProfile);

            if (syncToServer)
            {
                AdaptiveCompanionAI.SendManualProfilePacket((byte)Player.whoAmI, ManualPowerProfileEnabled, ManualPowerProfile);
            }
        }

        public void RecordDuelResult(int durationSeconds, int playerDamageDealt, int companionDamageDealt, bool companionWon)
        {
            EnsureInitialized();
            DuelHistory.Insert(0, new DuelRecord
            {
                DurationSeconds = System.Math.Max(0, durationSeconds),
                PlayerDamageDealt = System.Math.Max(0, playerDamageDealt),
                CompanionDamageDealt = System.Math.Max(0, companionDamageDealt),
                Winner = companionWon ? "Компаньон" : "Игрок",
            });

            while (DuelHistory.Count > MaxDuelHistoryRecords)
            {
                DuelHistory.RemoveAt(DuelHistory.Count - 1);
            }

            PlayerMetrics.Set("last_duel_duration_seconds", durationSeconds);
            PlayerMetrics.Set("last_duel_player_damage", playerDamageDealt);
            PlayerMetrics.Set("last_duel_companion_damage", companionDamageDealt);
        }


        public void CompanionDealtDamage(int damage, bool boss)
        {
            CompanionMetrics.Increment("damage_dealt_total", damage);
            if (boss)
            {
                CompanionMetrics.Increment("boss_damage_total", damage);
            }
        }

        public void CompanionTookDamage(int damage)
        {
            CompanionMetrics.Increment("damage_taken_total", damage);
        }

        public void CompanionKilledTarget()
        {
            CompanionMetrics.Increment("hostile_kills_total");
        }

        public void CompanionFiredProjectile()
        {
            CompanionMetrics.Increment("projectiles_fired_total");
        }

        public void CompanionDidMeleeAction()
        {
            CompanionMetrics.Increment("melee_actions_total");
        }

        public void CompanionTeleported()
        {
            CompanionMetrics.Increment("teleports_total");
        }

        public void CompanionSwitchedTarget()
        {
            CompanionMetrics.Increment("target_switches_total");
        }

        public void CompanionWonDuel()
        {
            CompanionMetrics.Increment("duel_wins");
        }

        public void CompanionLostDuel()
        {
            CompanionMetrics.Increment("duel_losses");
        }

        public void UpdateCompanionRuntimeMetrics(NPC companionNpc, bool duelActive, float ownerDistance)
        {
            EnsureInitialized();

            CompanionMetrics.Increment("uptime_seconds", CompanionMath.SecondsPerTick);
            CompanionMetrics.Set("life_current", companionNpc.life);
            CompanionMetrics.Set("life_max_current", companionNpc.lifeMax);
            CompanionMetrics.Set("damage_current", companionNpc.damage);
            CompanionMetrics.Set("defense_current", companionNpc.defense);
            CompanionMetrics.Set("duel_state_current", duelActive ? 1d : 0d);
            CompanionMetrics.Set("storage_slots_used", CompanionInventory.StorageUsedSlots);
            CompanionMetrics.Set("weapon_slots_used", CompanionInventory.WeaponSlotsUsed);
            CompanionMetrics.Set("ammo_slots_used", CompanionInventory.AmmoSlotsUsed);
            CompanionMetrics.Set("armor_slots_used", CompanionInventory.ArmorUsedSlots);
            CompanionMetrics.Set("accessory_slots_used", CompanionInventory.AccessoryUsedSlots);
            CompanionMetrics.Set("hidden_armor_slots", CompanionInventory.HiddenArmorSlots);
            CompanionMetrics.Set("hidden_accessory_slots", CompanionInventory.HiddenAccessorySlots);
            CompanionMetrics.Set("owner_distance_current", ownerDistance);
            CompanionMetrics.Set("life_ratio_current", companionNpc.lifeMax > 0 ? companionNpc.life / (double)companionNpc.lifeMax : 0d);
            CompanionMetrics.Set("auto_coefficient_current", CurrentPowerSnapshot.AutoCoefficient);
            CompanionMetrics.Set("manual_coefficient_current", CurrentPowerSnapshot.ManualCoefficient);
            CompanionMetrics.Set("effective_coefficient_current", CurrentPowerSnapshot.EffectiveCoefficient);
            CompanionMetrics.Set("duel_coefficient_current", CurrentPowerSnapshot.DuelCoefficient);
            CompanionMetrics.Set("player_skill_score_current", CurrentPowerSnapshot.PlayerSkillScore * 100d);
            CompanionMetrics.Set("player_assistance_need_current", CurrentPowerSnapshot.AssistanceNeed * 100d);
            CompanionMetrics.Set("style_code", CurrentPowerSnapshot.PreferredStyleCode);
            CompanionMetrics.Set("skill_simulation_mode", (int)SkillSimulationMode);
            CompanionMetrics.Set("manual_style_enabled", ManualCombatStyleEnabled ? 1d : 0d);
            CompanionMetrics.Set("manual_profile_enabled", ManualPowerProfileEnabled ? 1d : 0d);

            _followDistanceAccumulator += ownerDistance;
            _followDistanceSamples++;
            CompanionMetrics.Set("average_follow_distance", _followDistanceSamples > 0 ? _followDistanceAccumulator / _followDistanceSamples : 0d);
        }

        public void UpdateCompanionTacticalMetrics(int weaponDamage, int weaponUseTime, float weaponRange, int attackMode, float targetDistance, int targetLife, float threatLevel, float preferredDistance)
        {
            EnsureInitialized();
            CompanionMetrics.Set("selected_weapon_damage", weaponDamage);
            CompanionMetrics.Set("selected_weapon_use_time", weaponUseTime);
            CompanionMetrics.Set("selected_weapon_range", weaponRange);
            CompanionMetrics.Set("selected_attack_mode", attackMode);
            CompanionMetrics.Set("target_distance_current", targetDistance);
            CompanionMetrics.Set("target_life_current", targetLife);
            CompanionMetrics.Set("threat_level_current", threatLevel);
            CompanionMetrics.Set("tactical_distance_preference", preferredDistance);
        }

        private void EnsureInitialized()
        {
            if (PlayerMetrics == null || CompanionMetrics == null || CompanionInventory == null)
            {
                Initialize();
            }
        }

        private void ResetRuntimeTracking()
        {
            _lastPosition = Player.position;
            _positionInitialized = false;
            _lastLife = Player.statLife;
            _lastMana = Player.statMana;
            _lastChestIndex = Player.chest;
            _lastPotionDelay = Player.potionDelay;
            _lastControlMask = 0;
            _lastHorizontalIntent = 0;
            _speedAccumulator = 0d;
            _speedSamples = 0;
            _followDistanceAccumulator = 0d;
            _followDistanceSamples = 0;
            _wasLowHealth = false;
        }

        private void UpdateEnvironmentMetrics()
        {
            if (Player.wet)
            {
                PlayerMetrics.Increment("underwater_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.lavaWet)
            {
                PlayerMetrics.Increment("lava_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.honeyWet)
            {
                PlayerMetrics.Increment("honey_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneJungle)
            {
                PlayerMetrics.Increment("jungle_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneDesert)
            {
                PlayerMetrics.Increment("desert_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneSnow)
            {
                PlayerMetrics.Increment("snow_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneHallow)
            {
                PlayerMetrics.Increment("hallow_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneCorrupt || Player.ZoneCrimson)
            {
                PlayerMetrics.Increment("corruption_crimson_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneDungeon)
            {
                PlayerMetrics.Increment("dungeon_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneUnderworldHeight)
            {
                PlayerMetrics.Increment("underworld_time_seconds", CompanionMath.SecondsPerTick);
            }

            if (Player.ZoneDirtLayerHeight || Player.ZoneRockLayerHeight || Player.ZoneUnderworldHeight)
            {
                PlayerMetrics.Increment("underground_time_seconds", CompanionMath.SecondsPerTick);
            }
            else
            {
                PlayerMetrics.Increment("surface_time_seconds", CompanionMath.SecondsPerTick);
            }
        }

        private void UpdateResourceMetrics()
        {
            if (Player.statLife > _lastLife)
            {
                PlayerMetrics.Increment("healing_received_total", Player.statLife - _lastLife);
            }

            double lifeRatio = Player.statLifeMax2 > 0 ? Player.statLife / (double)Player.statLifeMax2 : 0d;
            if (!Player.dead && lifeRatio <= 0.35d)
            {
                PlayerMetrics.Increment("low_health_time_seconds", CompanionMath.SecondsPerTick);
            }

            bool lowNow = !Player.dead && lifeRatio <= 0.20d;
            if (lowNow && !_wasLowHealth)
            {
                PlayerMetrics.Increment("near_death_events_total");
            }

            _wasLowHealth = lowNow;

            if (Player.statMana > _lastMana)
            {
                PlayerMetrics.Increment("mana_regenerated_total", Player.statMana - _lastMana);
            }

            if (Player.potionDelay > _lastPotionDelay)
            {
                PlayerMetrics.Increment("potion_uses_total");
            }

            _lastLife = Player.statLife;
            _lastMana = Player.statMana;
            _lastPotionDelay = Player.potionDelay;
        }

        private void UpdateChestMetrics()
        {
            if (Player.chest >= 0 && _lastChestIndex < 0)
            {
                PlayerMetrics.Increment("chest_open_count");
            }

            _lastChestIndex = Player.chest;
        }

        private void UpdateCurrentSnapshotMetrics()
        {
            PlayerProgressSnapshot progress = PlayerProgressSnapshot.Capture(Player);
            double playTime = System.Math.Max(1d, PlayerMetrics.Get("play_time_seconds"));
            double playMinutes = playTime / 60d;
            double playHours = playTime / 3600d;
            double totalHits = PlayerMetrics.Get("melee_hits_total") + PlayerMetrics.Get("ranged_hits_total") + PlayerMetrics.Get("magic_hits_total") + PlayerMetrics.Get("summon_hits_total") + PlayerMetrics.Get("projectile_hits_total");
            double damageDealt = PlayerMetrics.Get("damage_dealt_total");
            double damageTaken = PlayerMetrics.Get("damage_taken_total");

            PlayerMetrics.Set("average_speed", _speedSamples > 0 ? _speedAccumulator / _speedSamples : 0d);
            PlayerMetrics.Set("inventory_fill_ratio_current", progress.InventoryFillRatio);
            PlayerMetrics.Set("armor_rating_current", progress.ArmorRating);
            PlayerMetrics.Set("defense_current", progress.Defense);
            PlayerMetrics.Set("life_current", Player.statLife);
            PlayerMetrics.Set("life_max_current", progress.LifeMax);
            PlayerMetrics.Set("life_ratio_current", progress.LifeMax > 0 ? Player.statLife / (double)progress.LifeMax : 0d);
            PlayerMetrics.Set("mana_current", Player.statMana);
            PlayerMetrics.Set("mana_max_current", progress.ManaMax);
            PlayerMetrics.Set("mana_ratio_current", progress.ManaMax > 0 ? Player.statMana / (double)progress.ManaMax : 0d);
            PlayerMetrics.Set("weapon_damage_current", progress.CurrentWeaponDamage);
            PlayerMetrics.Set("bosses_defeated_count_current", progress.BossesDefeated);
            PlayerMetrics.Set("progression_score", progress.ProgressRatio * 100d);
            PlayerMetrics.Set("input_rate", CompanionMath.SafeRate(PlayerMetrics.Get("active_key_load_total"), playTime + 1d));
            PlayerMetrics.Set("transition_rate", CompanionMath.SafeRate(PlayerMetrics.Get("key_transition_total"), playTime + 1d));
            PlayerMetrics.Set("apm_estimate", CompanionMath.SafeRate(PlayerMetrics.Get("key_transition_total") + PlayerMetrics.Get("item_use_presses_total"), playMinutes + 0.05d));
            PlayerMetrics.Set("damage_dealt_per_minute", CompanionMath.SafeRate(damageDealt, playMinutes + 0.05d));
            PlayerMetrics.Set("damage_taken_per_minute", CompanionMath.SafeRate(damageTaken, playMinutes + 0.05d));
            PlayerMetrics.Set("damage_efficiency_ratio", CompanionMath.SafeRate(damageDealt, damageTaken + 1d));
            PlayerMetrics.Set("healing_efficiency_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("healing_received_total"), damageTaken + 1d));
            PlayerMetrics.Set("kill_rate_per_minute", CompanionMath.SafeRate(PlayerMetrics.Get("npc_kills_total") + PlayerMetrics.Get("boss_kills_total"), playMinutes + 0.05d));
            PlayerMetrics.Set("deaths_per_hour", CompanionMath.SafeRate(PlayerMetrics.Get("death_count"), playHours + 0.05d));
            PlayerMetrics.Set("potion_uses_per_minute", CompanionMath.SafeRate(PlayerMetrics.Get("potion_uses_total"), playMinutes + 0.05d));
            PlayerMetrics.Set("jump_rate_per_minute", CompanionMath.SafeRate(PlayerMetrics.Get("jump_count"), playMinutes + 0.05d));
            PlayerMetrics.Set("boss_focus_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("boss_damage_total"), damageDealt + 1d));
            PlayerMetrics.Set("crit_rate_estimate", CompanionMath.SafeRate(PlayerMetrics.Get("crit_hits_total"), totalHits + 1d));
            PlayerMetrics.Set("close_combat_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("melee_hits_total"), totalHits + 1d));
            PlayerMetrics.Set("ranged_combat_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("ranged_hits_total"), totalHits + 1d));
            PlayerMetrics.Set("magic_combat_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("magic_hits_total"), totalHits + 1d));
            PlayerMetrics.Set("summon_combat_ratio", CompanionMath.SafeRate(PlayerMetrics.Get("summon_hits_total"), totalHits + 1d));
            PlayerMetrics.Set("biome_coverage_ratio", CompanionPowerModel.CalculateBiomeCoverage(PlayerMetrics, playTime));
            PlayerMetrics.Set("current_biome_danger_score", CalculateCurrentBiomeDanger());
        }

        private void UpdatePowerSnapshot()
        {
            CurrentPowerSnapshot = CompanionPowerModel.Evaluate(Player, this);
            CurrentCompanionStyle = CurrentPowerSnapshot.PreferredStyle;
            CurrentPowerProfile = CurrentPowerSnapshot.PowerProfile;
            PlayerMetrics.Set("difficulty_coefficient_auto", CurrentPowerSnapshot.AutoCoefficient);
            PlayerMetrics.Set("difficulty_coefficient_manual", CurrentPowerSnapshot.ManualCoefficient);
            PlayerMetrics.Set("difficulty_coefficient_effective", CurrentPowerSnapshot.EffectiveCoefficient);
            PlayerMetrics.Set("duel_coefficient_effective", CurrentPowerSnapshot.DuelCoefficient);
            PlayerMetrics.Set("skill_score_current", CurrentPowerSnapshot.PlayerSkillScore * 100d);
            PlayerMetrics.Set("assistance_need_current", CurrentPowerSnapshot.AssistanceNeed * 100d);
            PlayerMetrics.Set("combat_efficiency_score", CurrentPowerSnapshot.CombatScore * 100d);
            PlayerMetrics.Set("survival_score_current", CurrentPowerSnapshot.SurvivalScore * 100d);
            PlayerMetrics.Set("mobility_score_current", CurrentPowerSnapshot.MobilityScore * 100d);
            PlayerMetrics.Set("exploration_score_current", CurrentPowerSnapshot.ExplorationScore * 100d);
            PlayerMetrics.Set("gear_score_current", CurrentPowerSnapshot.GearScore * 100d);
            PlayerMetrics.Set("input_score_current", CurrentPowerSnapshot.InputScore * 100d);
            PlayerMetrics.Set("measured_skill_score_current", CurrentPowerSnapshot.MeasuredPlayerSkillScore * 100d);
            PlayerMetrics.Set("skill_simulation_mode", (int)SkillSimulationMode);
            PlayerMetrics.Set("manual_style_enabled", ManualCombatStyleEnabled ? 1d : 0d);
            PlayerMetrics.Set("manual_style_code", (int)ManualCombatStyle);
            PlayerMetrics.Set("manual_profile_enabled", ManualPowerProfileEnabled ? 1d : 0d);
            PlayerMetrics.Set("manual_profile_code", (int)ManualPowerProfile);
        }

        private double CalculateCurrentBiomeDanger()
        {
            double danger = 0d;
            if (Player.ZoneUnderworldHeight) danger += 35d;
            if (Player.ZoneDungeon) danger += 25d;
            if (Player.ZoneCorrupt || Player.ZoneCrimson) danger += 20d;
            if (Player.ZoneHallow) danger += 12d;
            if (Player.lavaWet) danger += 20d;
            if (Player.wet) danger += 5d;
            return CompanionMath.Clamp(danger, 0d, 100d);
        }

        private static float NormalizeManualScalar(float value)
        {
            return CompanionMath.SnapToStep(value, 0d, 10d, 0.5d);
        }

        private static CompanionSkillSimulationMode NormalizeSkillMode(int value)
        {
            return value >= 0 && value <= 3 ? (CompanionSkillSimulationMode)value : CompanionSkillSimulationMode.Real;
        }

        private static CompanionCombatStyle NormalizeCombatStyle(int value)
        {
            return value >= 0 && value <= 4 ? (CompanionCombatStyle)value : CompanionCombatStyle.Balanced;
        }

        private static CompanionPowerProfile NormalizePowerProfile(int value)
        {
            return value >= 0 && value <= 3 ? (CompanionPowerProfile)value : CompanionPowerProfile.Balanced;
        }

        private List<Terraria.ModLoader.IO.TagCompound> SaveDuelHistory()
        {
            List<Terraria.ModLoader.IO.TagCompound> tags = new List<Terraria.ModLoader.IO.TagCompound>(DuelHistory.Count);
            foreach (DuelRecord record in DuelHistory)
            {
                tags.Add(record.SaveTag());
            }

            return tags;
        }

        private void LoadDuelHistory(IList<Terraria.ModLoader.IO.TagCompound> saved)
        {
            DuelHistory ??= new List<DuelRecord>(MaxDuelHistoryRecords);
            DuelHistory.Clear();

            if (saved == null)
            {
                return;
            }

            for (int i = 0; i < saved.Count && DuelHistory.Count < MaxDuelHistoryRecords; i++)
            {
                DuelHistory.Add(DuelRecord.FromTag(saved[i]));
            }
        }

        private void TryAutoSpawnCompanion()
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
            {
                return;
            }

            AdaptiveCompanionConfig config = ModContent.GetInstance<AdaptiveCompanionConfig>();
            if (!config.AutoSpawnOnEnterWorld)
            {
                return;
            }

            if (_spawnRetryCooldown > 0)
            {
                _spawnRetryCooldown--;
                return;
            }

            if (!AdaptiveCompanionSystem.HasOwnedCompanion(Player, out _))
            {
                AdaptiveCompanionSystem.SpawnOrRecallCompanion(Player, false);
                _spawnRetryCooldown = GetRespawnDelayTicks();
            }
        }

        private static int GetRespawnDelayTicks()
        {
            AdaptiveCompanionConfig config = ModContent.GetInstance<AdaptiveCompanionConfig>();
            return System.Math.Max(60, config.RespawnDelaySeconds * 60);
        }
    }
}
