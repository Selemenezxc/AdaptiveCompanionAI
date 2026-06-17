using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaptiveCompanionAI.Common.Data
{
    public sealed class MetricDescriptor
    {
        public MetricDescriptor(string key, string label, string format = "0.##")
        {
            Key = key;
            Label = label;
            Format = format;
        }

        public string Key { get; }
        public string Label { get; }
        public string Format { get; }

        public string FormatValue(double value)
        {
            return value.ToString(Format);
        }
    }

    public static class MetricCatalog
    {
        public const int PageSize = 12;

        public static readonly IReadOnlyList<MetricDescriptor> Player = new List<MetricDescriptor>
        {
            new MetricDescriptor("skill_score_current", "Оценка навыка игрока", "0"),
            new MetricDescriptor("measured_skill_score_current", "Реальный навык без имитации", "0"),
            new MetricDescriptor("skill_simulation_mode", "Режим имитации навыка", "0"),
            new MetricDescriptor("assistance_need_current", "Потребность в помощи", "0"),
            new MetricDescriptor("combat_efficiency_score", "Боевой индекс", "0"),
            new MetricDescriptor("survival_score_current", "Индекс выживаемости", "0"),
            new MetricDescriptor("mobility_score_current", "Индекс мобильности", "0"),
            new MetricDescriptor("exploration_score_current", "Индекс исследования", "0"),
            new MetricDescriptor("gear_score_current", "Индекс снаряжения", "0"),
            new MetricDescriptor("input_score_current", "Индекс управления", "0"),
            new MetricDescriptor("play_time_seconds", "Время в игре, сек"),
            new MetricDescriptor("alive_time_seconds", "Время в живом состоянии, сек"),
            new MetricDescriptor("death_count", "Количество смертей", "0"),
            new MetricDescriptor("respawn_count", "Количество возрождений", "0"),
            new MetricDescriptor("deaths_per_hour", "Смертей в час"),
            new MetricDescriptor("near_death_events_total", "Критические просадки HP", "0"),
            new MetricDescriptor("low_health_time_seconds", "Время с низким HP, сек"),
            new MetricDescriptor("damage_dealt_total", "Суммарный нанесенный урон", "0"),
            new MetricDescriptor("damage_taken_total", "Суммарный полученный урон", "0"),
            new MetricDescriptor("damage_dealt_per_minute", "Урон в минуту"),
            new MetricDescriptor("damage_taken_per_minute", "Полученный урон в минуту"),
            new MetricDescriptor("damage_efficiency_ratio", "Соотношение урон/получено"),
            new MetricDescriptor("healing_received_total", "Суммарное восстановление HP", "0"),
            new MetricDescriptor("healing_efficiency_ratio", "Лечение к полученному урону"),
            new MetricDescriptor("mana_spent_total", "Суммарная трата маны", "0"),
            new MetricDescriptor("mana_regenerated_total", "Суммарное восстановление маны", "0"),
            new MetricDescriptor("npc_kills_total", "Убитые обычные NPC", "0"),
            new MetricDescriptor("kill_rate_per_minute", "Убийств в минуту"),
            new MetricDescriptor("boss_kills_total", "Убитые боссы", "0"),
            new MetricDescriptor("boss_damage_total", "Урон по боссам", "0"),
            new MetricDescriptor("boss_focus_ratio", "Доля урона по боссам"),
            new MetricDescriptor("crit_hits_total", "Критические попадания", "0"),
            new MetricDescriptor("crit_rate_estimate", "Оценка частоты критов"),
            new MetricDescriptor("melee_hits_total", "Попадания ближнего боя", "0"),
            new MetricDescriptor("ranged_hits_total", "Попадания дальнего боя", "0"),
            new MetricDescriptor("magic_hits_total", "Попадания магией", "0"),
            new MetricDescriptor("summon_hits_total", "Попадания призывом", "0"),
            new MetricDescriptor("projectile_hits_total", "Попадания снарядами", "0"),
            new MetricDescriptor("close_combat_ratio", "Доля ближнего боя"),
            new MetricDescriptor("ranged_combat_ratio", "Доля дальнего боя"),
            new MetricDescriptor("magic_combat_ratio", "Доля магии"),
            new MetricDescriptor("summon_combat_ratio", "Доля призыва"),
            new MetricDescriptor("movement_distance_total", "Пройденная дистанция, px"),
            new MetricDescriptor("horizontal_distance_total", "Горизонтальная дистанция, px"),
            new MetricDescriptor("vertical_distance_total", "Вертикальная дистанция, px"),
            new MetricDescriptor("max_speed_observed", "Максимальная скорость"),
            new MetricDescriptor("average_speed", "Средняя скорость"),
            new MetricDescriptor("jump_count", "Количество прыжков", "0"),
            new MetricDescriptor("jump_rate_per_minute", "Прыжков в минуту"),
            new MetricDescriptor("mount_time_seconds", "Время на маунте, сек"),
            new MetricDescriptor("flight_time_seconds", "Время в полете, сек"),
            new MetricDescriptor("underwater_time_seconds", "Время под водой, сек"),
            new MetricDescriptor("lava_time_seconds", "Время в лаве, сек"),
            new MetricDescriptor("honey_time_seconds", "Время в меду, сек"),
            new MetricDescriptor("surface_time_seconds", "Время на поверхности, сек"),
            new MetricDescriptor("underground_time_seconds", "Время под землей, сек"),
            new MetricDescriptor("jungle_time_seconds", "Время в джунглях, сек"),
            new MetricDescriptor("desert_time_seconds", "Время в пустыне, сек"),
            new MetricDescriptor("snow_time_seconds", "Время в снегу, сек"),
            new MetricDescriptor("hallow_time_seconds", "Время в святых землях, сек"),
            new MetricDescriptor("corruption_crimson_time_seconds", "Время в порче/кримзоне, сек"),
            new MetricDescriptor("dungeon_time_seconds", "Время в данже, сек"),
            new MetricDescriptor("underworld_time_seconds", "Время в аду, сек"),
            new MetricDescriptor("biome_coverage_ratio", "Разнообразие биомов"),
            new MetricDescriptor("current_biome_danger_score", "Опасность текущего биома", "0"),
            new MetricDescriptor("chest_open_count", "Открытия сундуков", "0"),
            new MetricDescriptor("item_pickups_total", "Поднятые предметы", "0"),
            new MetricDescriptor("coin_value_picked_total", "Поднятые монеты, медь", "0"),
            new MetricDescriptor("active_key_load_total", "Суммарная нагрузка по клавишам", "0"),
            new MetricDescriptor("key_transition_total", "Переключения клавиш", "0"),
            new MetricDescriptor("reaction_change_total", "Смены направления", "0"),
            new MetricDescriptor("item_use_presses_total", "Нажатия атаки/использования", "0"),
            new MetricDescriptor("quick_heal_key_presses_total", "Нажатия быстрого лечения", "0"),
            new MetricDescriptor("quick_mana_key_presses_total", "Нажатия быстрой маны", "0"),
            new MetricDescriptor("potion_uses_total", "Использования зелий", "0"),
            new MetricDescriptor("potion_uses_per_minute", "Зелий в минуту"),
            new MetricDescriptor("input_rate", "Средняя интенсивность ввода"),
            new MetricDescriptor("transition_rate", "Средняя скорость переключений"),
            new MetricDescriptor("apm_estimate", "Оценка APM"),
            new MetricDescriptor("inventory_fill_ratio_current", "Заполнение инвентаря"),
            new MetricDescriptor("armor_rating_current", "Суммарная защита экипировки"),
            new MetricDescriptor("defense_current", "Текущая защита"),
            new MetricDescriptor("life_current", "Текущее здоровье", "0"),
            new MetricDescriptor("life_max_current", "Максимум здоровья", "0"),
            new MetricDescriptor("life_ratio_current", "Доля здоровья"),
            new MetricDescriptor("mana_current", "Текущая мана", "0"),
            new MetricDescriptor("mana_max_current", "Максимум маны", "0"),
            new MetricDescriptor("mana_ratio_current", "Доля маны"),
            new MetricDescriptor("weapon_damage_current", "Урон активного предмета", "0"),
            new MetricDescriptor("bosses_defeated_count_current", "Побеждено прогресс-боссов", "0"),
            new MetricDescriptor("progression_score", "Индекс прогресса", "0"),
            new MetricDescriptor("difficulty_coefficient_auto", "Авто-коэффициент поддержки"),
            new MetricDescriptor("difficulty_coefficient_manual", "Ручной коэффициент"),
            new MetricDescriptor("difficulty_coefficient_effective", "Итоговая сила поддержки"),
            new MetricDescriptor("duel_coefficient_effective", "Итоговая сила в дуэли"),
            new MetricDescriptor("manual_style_enabled", "Ручной стиль боя", "0"),
            new MetricDescriptor("manual_style_code", "Код ручного стиля", "0"),
            new MetricDescriptor("manual_profile_enabled", "Ручной профиль силы", "0"),
            new MetricDescriptor("manual_profile_code", "Код ручного профиля", "0"),
            new MetricDescriptor("last_duel_duration_seconds", "Последняя дуэль: время", "0"),
            new MetricDescriptor("last_duel_player_damage", "Последняя дуэль: урон игрока", "0"),
            new MetricDescriptor("last_duel_companion_damage", "Последняя дуэль: урон компаньона", "0"),
        };

        public static readonly IReadOnlyList<MetricDescriptor> Companion = new List<MetricDescriptor>
        {
            new MetricDescriptor("uptime_seconds", "Время активности, сек"),
            new MetricDescriptor("damage_dealt_total", "Нанесенный урон", "0"),
            new MetricDescriptor("damage_taken_total", "Полученный урон", "0"),
            new MetricDescriptor("hostile_kills_total", "Убитые враги", "0"),
            new MetricDescriptor("boss_damage_total", "Урон по боссам", "0"),
            new MetricDescriptor("projectiles_fired_total", "Выпущенные снаряды", "0"),
            new MetricDescriptor("melee_actions_total", "Ближние атаки", "0"),
            new MetricDescriptor("teleports_total", "Телепортации к игроку", "0"),
            new MetricDescriptor("target_switches_total", "Смены цели", "0"),
            new MetricDescriptor("duel_wins", "Победы в дуэлях", "0"),
            new MetricDescriptor("duel_losses", "Поражения в дуэлях", "0"),
            new MetricDescriptor("duel_state_current", "Состояние дуэли"),
            new MetricDescriptor("storage_slots_used", "Заполненные слоты хранилища", "0"),
            new MetricDescriptor("weapon_slots_used", "Оружие в хранилище", "0"),
            new MetricDescriptor("ammo_slots_used", "Боеприпасы в хранилище", "0"),
            new MetricDescriptor("armor_slots_used", "Заполненные слоты брони", "0"),
            new MetricDescriptor("accessory_slots_used", "Заполненные слоты аксессуаров", "0"),
            new MetricDescriptor("hidden_armor_slots", "Скрытые слоты брони", "0"),
            new MetricDescriptor("hidden_accessory_slots", "Скрытые аксессуары", "0"),
            new MetricDescriptor("life_current", "Текущее здоровье", "0"),
            new MetricDescriptor("life_max_current", "Максимум здоровья", "0"),
            new MetricDescriptor("life_ratio_current", "Доля здоровья"),
            new MetricDescriptor("damage_current", "Текущий урон", "0"),
            new MetricDescriptor("defense_current", "Текущая защита", "0"),
            new MetricDescriptor("owner_distance_current", "Дистанция до игрока"),
            new MetricDescriptor("average_follow_distance", "Средняя дистанция сопровождения"),
            new MetricDescriptor("target_distance_current", "Дистанция до цели"),
            new MetricDescriptor("target_life_current", "Здоровье цели", "0"),
            new MetricDescriptor("threat_level_current", "Угроза цели", "0"),
            new MetricDescriptor("selected_weapon_damage", "Урон выбранного оружия", "0"),
            new MetricDescriptor("selected_weapon_use_time", "Скорость выбранного оружия", "0"),
            new MetricDescriptor("selected_weapon_range", "Дистанция выбранного оружия"),
            new MetricDescriptor("selected_attack_mode", "Код режима атаки", "0"),
            new MetricDescriptor("tactical_distance_preference", "Желаемая дистанция"),
            new MetricDescriptor("auto_coefficient_current", "Авто-коэффициент поддержки"),
            new MetricDescriptor("manual_coefficient_current", "Ручной коэффициент"),
            new MetricDescriptor("effective_coefficient_current", "Итоговая сила поддержки"),
            new MetricDescriptor("duel_coefficient_current", "Итоговая сила в дуэли"),
            new MetricDescriptor("player_skill_score_current", "Навык игрока для ИИ", "0"),
            new MetricDescriptor("player_assistance_need_current", "Помощь, нужная игроку", "0"),
            new MetricDescriptor("style_code", "Код боевого профиля"),
            new MetricDescriptor("interface_leash_active", "Ограничение радиуса интерфейса", "0"),
            new MetricDescriptor("skill_simulation_mode", "Имитация навыка игрока", "0"),
            new MetricDescriptor("manual_style_enabled", "Ручной стиль боя", "0"),
            new MetricDescriptor("manual_profile_enabled", "Ручной профиль силы", "0"),
        };

        public static IReadOnlyList<MetricDescriptor> GetPage(IReadOnlyList<MetricDescriptor> descriptors, int pageIndex)
        {
            int pageCount = GetPageCount(descriptors);
            pageIndex = Math.Max(0, Math.Min(pageIndex, pageCount - 1));
            return descriptors.Skip(pageIndex * PageSize).Take(PageSize).ToList();
        }

        public static int GetPageCount(IReadOnlyList<MetricDescriptor> descriptors)
        {
            return Math.Max(1, (int)Math.Ceiling(descriptors.Count / (double)PageSize));
        }
    }
}
