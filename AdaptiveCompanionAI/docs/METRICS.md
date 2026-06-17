# Metrics

The project uses two metric snapshots: one for the player and one for the companion. Metrics are intentionally explicit and serializable so they can be referenced in the graduation-work text, screenshots and test protocol.

## Player metric groups

### Skill and adaptation

- `skill_score_current`
- `measured_skill_score_current`
- `skill_simulation_mode`
- `assistance_need_current`
- `combat_efficiency_score`
- `survival_score_current`
- `mobility_score_current`
- `exploration_score_current`
- `gear_score_current`
- `input_score_current`
- `difficulty_coefficient_auto`
- `difficulty_coefficient_manual`
- `difficulty_coefficient_effective`
- `duel_coefficient_effective`

### Survival

- play/alive time;
- deaths and respawns;
- damage taken;
- healing received;
- low-health time;
- near-death events;
- current/max HP and HP ratio;
- current/max mana and mana ratio.

### Combat

- total damage;
- damage per minute;
- kills and kill rate;
- boss kills and boss damage;
- estimated crit rate;
- melee/ranged/magic/summon/projectile hit counters;
- combat-style ratios;
- active weapon damage.

### Movement and exploration

- total/horizontal/vertical movement;
- max and average speed;
- jump rate;
- mount time;
- wing/flight time;
- biome time counters;
- chest openings;
- item pickups and coin value.

### Input behavior

- active key load;
- key transitions;
- direction changes;
- item-use presses;
- quick heal/mana presses;
- potion usage rate;
- APM estimate.

## Companion metrics

### Runtime

- `uptime_seconds`
- `life_current`
- `life_max_current`
- `life_ratio_current`
- `damage_current`
- `defense_current`
- `owner_distance_current`
- `average_follow_distance`
- `interface_leash_active`

### Inventory and equipment

- `storage_slots_used`
- `weapon_slots_used`
- `ammo_slots_used`
- `armor_slots_used`
- `accessory_slots_used`
- `hidden_armor_slots`
- `hidden_accessory_slots`

### Combat decisions

- `selected_weapon_damage`
- `selected_weapon_use_time`
- `selected_weapon_range`
- `selected_attack_mode`
- `target_distance_current`
- `target_life_current`
- `threat_level_current`
- `tactical_distance_preference`
- `projectiles_fired_total`
- `melee_actions_total`
- `damage_dealt_total`
- `damage_taken_total`
- `hostile_kills_total`
- `boss_damage_total`

### Adaptation state

- `auto_coefficient_current`
- `manual_coefficient_current`
- `effective_coefficient_current`
- `duel_coefficient_current`
- `player_skill_score_current`
- `player_assistance_need_current`
- `style_code`
- `skill_simulation_mode`
- `manual_style_enabled`
- `manual_profile_enabled`

## How metrics drive behavior

1. Player metrics are normalized into partial scores.
2. Partial scores form `PlayerSkillScore` and `AssistanceNeed`.
3. The support coefficient increases when the player struggles.
4. The duel coefficient is calculated separately to keep the duel useful as a calibration scenario.
5. Combat style metrics affect automatic weapon preference unless the user forces a manual style.
6. Companion metrics expose the current decision so behavior can be inspected during testing.
