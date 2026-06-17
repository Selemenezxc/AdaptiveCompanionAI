using System;
using System.Collections.Generic;
using AdaptiveCompanionAI.Common.Data;
using AdaptiveCompanionAI.Common.Players;
using AdaptiveCompanionAI.Common.Systems;
using AdaptiveCompanionAI.Content.NPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace AdaptiveCompanionAI.Common.UI
{
    public class CompanionUIState : UIState
    {
        private enum CompanionTab
        {
            Inventory,
            Metrics,
            Battle,
            Settings,
        }

        private UIDragPanel _panel;
        private UIPanel _contentPanel;
        private UIText _headerText;
        private UITextPanel<string> _closeButton;
        private UITextPanel<string> _inventoryTabButton;
        private UITextPanel<string> _metricsTabButton;
        private UITextPanel<string> _battleTabButton;
        private UITextPanel<string> _settingsTabButton;

        private CompanionTab _currentTab = CompanionTab.Inventory;
        private int _npcIndex = -1;
        private bool _needsRebuild = true;
        private int _dynamicRefreshTick;

        public override void OnInitialize()
        {
            _panel = new UIDragPanel();
            _panel.Width.Set(980f, 0f);
            _panel.Height.Set(650f, 0f);
            _panel.Left.Set((Main.screenWidth - 980f) * 0.5f, 0f);
            _panel.Top.Set((Main.screenHeight - 650f) * 0.5f, 0f);
            Append(_panel);

            _headerText = new UIText("Адаптивный компаньон", 0.95f);
            _headerText.IgnoresMouseInteraction = true;
            _headerText.Left.Set(330f, 0f);
            _headerText.Top.Set(12f, 0f);
            _headerText.Width.Set(360f, 0f);
            _panel.Append(_headerText);

            _closeButton = CreateHeaderButton("X", 912f, 8f, 44f, 28f, Close);
            _panel.Append(_closeButton);

            _inventoryTabButton = CreateTabButton("Инвентарь", 58f, CompanionTab.Inventory);
            _metricsTabButton = CreateTabButton("Метрики", 98f, CompanionTab.Metrics);
            _battleTabButton = CreateTabButton("Битва", 138f, CompanionTab.Battle);
            _settingsTabButton = CreateTabButton("Настройка", 178f, CompanionTab.Settings);
            _panel.Append(_inventoryTabButton);
            _panel.Append(_metricsTabButton);
            _panel.Append(_battleTabButton);
            _panel.Append(_settingsTabButton);

            _contentPanel = new UIPanel();
            _contentPanel.Left.Set(148f, 0f);
            _contentPanel.Top.Set(48f, 0f);
            _contentPanel.Width.Set(812f, 0f);
            _contentPanel.Height.Set(586f, 0f);
            _contentPanel.BackgroundColor = new Color(24, 30, 54) * 0.96f;
            _contentPanel.BorderColor = new Color(89, 116, 213) * 0.9f;
            _panel.Append(_contentPanel);

            UpdateTabVisuals(false);
        }

        public void Bind(int npcIndex)
        {
            _npcIndex = npcIndex;
            _needsRebuild = true;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Main.playerInventory)
            {
                Close();
                return;
            }

            if (!TryGetBoundCompanion(out NPC npc, out AdaptiveCompanionNPC companion, out Player owner, out AdaptiveDifficultyPlayer data))
            {
                Close();
                return;
            }

            bool fullAccess = AdaptiveCompanionSystem.IsCompanionClose(owner, npc);
            if (!fullAccess && _currentTab != CompanionTab.Inventory)
            {
                _currentTab = CompanionTab.Inventory;
                _needsRebuild = true;
            }

            UpdateTabVisuals(fullAccess);

            // Do not rebuild scrollable tabs on a timer: rebuilding UIList/UIScrollbar resets
            // the current scroll position. Content is rebuilt when the tab is opened or a button
            // changes state, which keeps long metric and duel-history lists usable.
            _dynamicRefreshTick = 0;

            if (_needsRebuild)
            {
                RebuildContent(npc, companion, owner, data, fullAccess);
            }
        }

        private UITextPanel<string> CreateHeaderButton(string text, float left, float top, float width, float height, Action onClick)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, 0.85f, false);
            button.Left.Set(left, 0f);
            button.Top.Set(top, 0f);
            button.Width.Set(width, 0f);
            button.Height.Set(height, 0f);
            button.PaddingTop = 4f;
            button.PaddingBottom = 4f;
            ApplyButtonStyle(button, true, false);
            button.OnLeftClick += (_, _) => onClick();
            return button;
        }

        private UITextPanel<string> CreateTabButton(string text, float top, CompanionTab tab)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, 0.85f, false);
            button.Left.Set(14f, 0f);
            button.Top.Set(top, 0f);
            button.Width.Set(122f, 0f);
            button.Height.Set(32f, 0f);
            button.PaddingTop = 6f;
            button.PaddingBottom = 4f;
            button.OnLeftClick += (_, _) =>
            {
                if (!CanSelectTab(tab))
                {
                    return;
                }

                _currentTab = tab;
                _needsRebuild = true;
            };
            return button;
        }

        private UITextPanel<string> CreateContentButton(string text, float left, float top, float width, float height, Action onClick, bool enabled = true, bool selected = false)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, 0.78f, false);
            button.Left.Set(left, 0f);
            button.Top.Set(top, 0f);
            button.Width.Set(width, 0f);
            button.Height.Set(height, 0f);
            button.PaddingTop = 5f;
            button.PaddingBottom = 4f;
            ApplyButtonStyle(button, enabled, selected);
            if (enabled)
            {
                button.OnLeftClick += (_, _) => onClick();
            }

            return button;
        }

        private UIElement CreateToggleSwitch(string label, float left, float top, float width, bool enabled, bool active, Action onClick)
        {
            UIPanel wrapper = new UIPanel();
            wrapper.Left.Set(left, 0f);
            wrapper.Top.Set(top, 0f);
            wrapper.Width.Set(width, 0f);
            wrapper.Height.Set(32f, 0f);
            wrapper.SetPadding(0f);
            wrapper.BackgroundColor = enabled ? new Color(33, 43, 79) * 0.78f : new Color(48, 52, 68) * 0.72f;
            wrapper.BorderColor = enabled ? new Color(95, 125, 225) * 0.70f : new Color(85, 88, 104) * 0.72f;

            if (enabled)
            {
                wrapper.OnLeftClick += (_, _) => onClick();
            }

            wrapper.Append(CreateText(label, 10f, 8f, 0.72f, enabled ? Color.White : new Color(150, 154, 166)));

            UIPanel track = new UIPanel();
            track.Left.Set(width - 58f, 0f);
            track.Top.Set(6f, 0f);
            track.Width.Set(46f, 0f);
            track.Height.Set(20f, 0f);
            track.SetPadding(0f);
            track.IgnoresMouseInteraction = true;
            track.BackgroundColor = !enabled ? new Color(60, 62, 74) * 0.85f : active ? new Color(103, 130, 220) * 0.95f : new Color(62, 66, 82) * 0.92f;
            track.BorderColor = !enabled ? new Color(85, 88, 104) * 0.72f : active ? new Color(182, 205, 255) : new Color(122, 157, 255) * 0.72f;

            UIPanel knob = new UIPanel();
            knob.Left.Set(active ? 23f : 3f, 0f);
            knob.Top.Set(3f, 0f);
            knob.Width.Set(14f, 0f);
            knob.Height.Set(14f, 0f);
            knob.SetPadding(0f);
            knob.IgnoresMouseInteraction = true;
            knob.BackgroundColor = enabled ? Color.White * 0.92f : new Color(145, 145, 152) * 0.82f;
            knob.BorderColor = Color.Transparent;

            track.Append(knob);
            wrapper.Append(track);
            return wrapper;
        }

        private UIText CreateText(string text, float left, float top, float scale = 0.85f, Color? color = null)
        {
            UIText uiText = new UIText(text, scale, false);
            uiText.IgnoresMouseInteraction = true;
            uiText.Left.Set(left, 0f);
            uiText.Top.Set(top, 0f);
            if (color.HasValue)
            {
                uiText.TextColor = color.Value;
            }

            return uiText;
        }

        private bool CanSelectTab(CompanionTab tab)
        {
            if (tab == CompanionTab.Inventory)
            {
                return true;
            }

            return TryGetBoundCompanion(out NPC npc, out _, out Player owner, out _) && AdaptiveCompanionSystem.IsCompanionClose(owner, npc);
        }

        private void UpdateTabVisuals(bool fullAccess)
        {
            SetTabColor(_inventoryTabButton, _currentTab == CompanionTab.Inventory, true);
            SetTabColor(_metricsTabButton, _currentTab == CompanionTab.Metrics, fullAccess);
            SetTabColor(_battleTabButton, _currentTab == CompanionTab.Battle, fullAccess);
            SetTabColor(_settingsTabButton, _currentTab == CompanionTab.Settings, fullAccess);
        }

        private static void SetTabColor(UITextPanel<string> button, bool active, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            if (!enabled)
            {
                button.BackgroundColor = new Color(47, 51, 70) * 0.88f;
                button.BorderColor = new Color(88, 92, 112) * 0.85f;
                return;
            }

            button.BackgroundColor = active ? new Color(103, 130, 220) * 0.98f : new Color(49, 64, 121) * 0.92f;
            button.BorderColor = new Color(122, 157, 255);
        }

        private static void ApplyButtonStyle(UITextPanel<string> button, bool enabled, bool selected)
        {
            if (!enabled)
            {
                button.BackgroundColor = new Color(48, 52, 68) * 0.84f;
                button.BorderColor = new Color(85, 88, 104) * 0.8f;
                return;
            }

            button.BackgroundColor = selected ? new Color(104, 132, 220) * 0.98f : new Color(63, 82, 151) * 0.9f;
            button.BorderColor = selected ? new Color(182, 205, 255) : new Color(122, 157, 255);
        }

        private void RebuildContent(NPC npc, AdaptiveCompanionNPC companion, Player owner, AdaptiveDifficultyPlayer data, bool fullAccess)
        {
            _contentPanel.RemoveAllChildren();
            _headerText.SetText(fullAccess ? "Адаптивный компаньон" : "Компаньон далеко");
            UpdateTabVisuals(fullAccess);

            switch (_currentTab)
            {
                case CompanionTab.Inventory:
                    BuildInventoryContent(npc, owner, data, fullAccess);
                    break;
                case CompanionTab.Metrics:
                    BuildMetricsContent(data);
                    break;
                case CompanionTab.Battle:
                    BuildBattleContent(npc, companion, owner, data);
                    break;
                case CompanionTab.Settings:
                    BuildSettingsContent(data);
                    break;
            }

            _needsRebuild = false;
        }

        private void OnCompanionSlotChanged()
        {
            _needsRebuild = true;
        }

        private static Func<Item, bool> GetArmorValidator(int slotIndex)
        {
            return slotIndex switch
            {
                0 => CompanionItemSlot.IsHeadArmor,
                1 => CompanionItemSlot.IsBodyArmor,
                2 => CompanionItemSlot.IsLegArmor,
                _ => null,
            };
        }

        private static string GetArmorRejectHint(int slotIndex)
        {
            return slotIndex switch
            {
                0 => "Только шлем или головной убор.",
                1 => "Только нагрудник.",
                2 => "Только поножи или обувь.",
                _ => "Этот предмет нельзя положить сюда.",
            };
        }

        private void BuildInventoryContent(NPC npc, Player owner, AdaptiveDifficultyPlayer data, bool fullAccess)
        {
            _contentPanel.Append(CreateText($"Инвентарь · {data.CompanionInventory.StorageUsedSlots}/40", 16f, 12f, 0.95f, new Color(255, 240, 170)));
            _contentPanel.Append(CreateContentButton("Вернуть рядом", 626f, 10f, 150f, 32f, () =>
            {
                AdaptiveCompanionSystem.SpawnOrRecallCompanion(owner, false);
                _needsRebuild = true;
            }, true));

            if (!fullAccess)
            {
                UIPanel locked = CreateInfoPanel(24f, 74f, 740f, 210f, "Доступ ограничен");
                locked.Append(CreateText("Компаньон далеко от игрока.", 18f, 52f, 0.86f));
                locked.Append(CreateText("Верни его рядом, чтобы открыть хранилище, снаряжение и настройки.", 18f, 86f, 0.76f, new Color(185, 205, 235)));
                _contentPanel.Append(locked);
                return;
            }


            const float startLeft = 18f;
            const float startTop = 68f;
            const float step = 54f;
            const int columns = 8;

            for (int i = 0; i < data.CompanionInventory.Storage.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;

                CompanionItemSlot slot = new CompanionItemSlot(data.CompanionInventory.Storage, i, ItemSlot.Context.ChestItem, null, null, OnCompanionSlotChanged);
                slot.Left.Set(startLeft + column * step, 0f);
                slot.Top.Set(startTop + row * step, 0f);
                _contentPanel.Append(slot);
            }

            UIPanel equipmentPanel = CreateInfoPanel(472f, 70f, 320f, 488f, "Снаряжение");
            BuildEquipmentRows(equipmentPanel, data);
            _contentPanel.Append(equipmentPanel);
        }

        private void BuildEquipmentRows(UIPanel equipmentPanel, AdaptiveDifficultyPlayer data)
        {
            string[] armorLabels = { "Голова", "Тело", "Ноги" };
            float top = 42f;
            for (int i = 0; i < data.CompanionInventory.Armor.Length; i++)
            {
                int slotIndex = i;
                CompanionItemSlot slot = new CompanionItemSlot(data.CompanionInventory.Armor, slotIndex, ItemSlot.Context.DisplayDollArmor, GetArmorValidator(slotIndex), GetArmorRejectHint(slotIndex), OnCompanionSlotChanged);
                slot.Left.Set(12f, 0f);
                slot.Top.Set(top, 0f);
                equipmentPanel.Append(slot);
                equipmentPanel.Append(CreateText(armorLabels[slotIndex], 72f, top + 14f, 0.76f));
                equipmentPanel.Append(CreateContentButton(data.CompanionInventory.IsArmorHidden(slotIndex) ? "скрыт" : "виден", 224f, top + 12f, 66f, 24f, () =>
                {
                    data.CompanionInventory.ToggleArmorVisibility(slotIndex);
                    _needsRebuild = true;
                }, true, !data.CompanionInventory.IsArmorHidden(slotIndex)));
                top += 48f;
            }

            top += 12f;
            for (int i = 0; i < data.CompanionInventory.Accessories.Length; i++)
            {
                int slotIndex = i;
                CompanionItemSlot slot = new CompanionItemSlot(data.CompanionInventory.Accessories, slotIndex, ItemSlot.Context.DisplayDollAccessory, CompanionItemSlot.IsAccessory, "Только аксессуар.", OnCompanionSlotChanged);
                slot.Left.Set(12f, 0f);
                slot.Top.Set(top, 0f);
                equipmentPanel.Append(slot);
                equipmentPanel.Append(CreateText($"Аксессуар {slotIndex + 1}", 72f, top + 14f, 0.74f));
                equipmentPanel.Append(CreateContentButton(data.CompanionInventory.IsAccessoryHidden(slotIndex) ? "скрыт" : "виден", 224f, top + 12f, 66f, 24f, () =>
                {
                    data.CompanionInventory.ToggleAccessoryVisibility(slotIndex);
                    _needsRebuild = true;
                }, true, !data.CompanionInventory.IsAccessoryHidden(slotIndex)));
                top += 42f;
            }
        }

        private void BuildMetricsContent(AdaptiveDifficultyPlayer data)
        {
            _contentPanel.Append(CreateText("Метрики", 16f, 12f, 0.95f, new Color(255, 240, 170)));
            _contentPanel.Append(CreateText($"Навык {data.CurrentPowerSnapshot.PlayerSkillScore * 100f:0}% · Помощь {data.CurrentPowerSnapshot.AssistanceNeed * 100f:0}% · {data.CurrentCompanionStyle}", 142f, 18f, 0.74f, new Color(200, 230, 255)));

            BuildMetricScrollColumn("Игрок", data.PlayerMetrics, MetricCatalog.Player, 16f, 58f, 380f, 498f);
            BuildMetricScrollColumn("Компаньон", data.CompanionMetrics, MetricCatalog.Companion, 414f, 58f, 382f, 498f);
        }

        private void BuildMetricScrollColumn(string title, MetricSnapshot snapshot, IReadOnlyList<MetricDescriptor> descriptors, float left, float top, float width, float height)
        {
            UIPanel panel = CreateInfoPanel(left, top, width, height, title);

            UIList list = new UIList();
            list.Left.Set(10f, 0f);
            list.Top.Set(42f, 0f);
            list.Width.Set(width - 38f, 0f);
            list.Height.Set(height - 54f, 0f);
            list.ListPadding = 4f;

            UIScrollbar scrollbar = new UIScrollbar();
            scrollbar.Left.Set(width - 22f, 0f);
            scrollbar.Top.Set(42f, 0f);
            scrollbar.Width.Set(16f, 0f);
            scrollbar.Height.Set(height - 54f, 0f);
            list.SetScrollbar(scrollbar);

            foreach (MetricDescriptor descriptor in descriptors)
            {
                list.Add(CreateMetricRow(descriptor.Label, descriptor.FormatValue(snapshot.Get(descriptor.Key))));
            }

            panel.Append(list);
            panel.Append(scrollbar);
            _contentPanel.Append(panel);
        }

        private UIElement CreateMetricRow(string label, string value)
        {
            UIPanel row = new UIPanel();
            row.Width.Set(0f, 1f);
            row.Height.Set(30f, 0f);
            row.BackgroundColor = new Color(28, 36, 68) * 0.62f;
            row.BorderColor = new Color(72, 96, 178) * 0.45f;
            row.SetPadding(0f);
            row.Append(CreateText(label, 8f, 6f, 0.68f));
            row.Append(CreateText(value, 260f, 6f, 0.68f, new Color(255, 240, 170)));
            return row;
        }

        private void BuildBattleContent(NPC npc, AdaptiveCompanionNPC companion, Player owner, AdaptiveDifficultyPlayer data)
        {
            _contentPanel.Append(CreateText("Битва", 16f, 12f, 0.95f, new Color(255, 240, 170)));

            float difficulty = MathHelper.Clamp(data.CurrentPowerSnapshot.DuelCoefficient / 3.15f, 0f, 1f);
            string difficultyText = ResolveDuelDifficultyText(data.CurrentPowerSnapshot.DuelCoefficient);
            _contentPanel.Append(CreateProgressBar($"Сложность: {difficultyText}", (int)(difficulty * 100f), 100, difficulty, 24f, 58f, 360f, new Color(120, 155, 245)));

            _contentPanel.Append(CreateContentButton(companion.DuelActive ? "Дуэль идет" : "Начать дуэль", 420f, 64f, 180f, 42f, () =>
            {
                AdaptiveCompanionSystem.ToggleDuel(owner, npc.whoAmI, true);
                Close();
            }, !companion.DuelActive));

            BuildDuelHistory(data, 24f, 136f, 760f, 410f);
        }

        private string ResolveDuelDifficultyText(float coefficient)
        {
            if (coefficient < 0.85f) return "низкая";
            if (coefficient < 1.45f) return "средняя";
            if (coefficient < 2.25f) return "высокая";
            return "экстремальная";
        }

        private void BuildDuelHistory(AdaptiveDifficultyPlayer data, float left, float top, float width, float height)
        {
            UIPanel panel = CreateInfoPanel(left, top, width, height, "История дуэлей");
            panel.Append(CreateText("Время", 16f, 42f, 0.72f, new Color(200, 230, 255)));
            panel.Append(CreateText("Урон И/К", 170f, 42f, 0.72f, new Color(200, 230, 255)));
            panel.Append(CreateText("Победитель", 386f, 42f, 0.72f, new Color(200, 230, 255)));

            UIList list = new UIList();
            list.Left.Set(10f, 0f);
            list.Top.Set(70f, 0f);
            list.Width.Set(width - 38f, 0f);
            list.Height.Set(height - 84f, 0f);
            list.ListPadding = 4f;

            UIScrollbar scrollbar = new UIScrollbar();
            scrollbar.Left.Set(width - 22f, 0f);
            scrollbar.Top.Set(70f, 0f);
            scrollbar.Width.Set(16f, 0f);
            scrollbar.Height.Set(height - 84f, 0f);
            list.SetScrollbar(scrollbar);

            if (data.DuelHistory.Count == 0)
            {
                list.Add(CreateHistoryRow("—", "Нет завершенных дуэлей", "—"));
            }
            else
            {
                foreach (DuelRecord record in data.DuelHistory)
                {
                    list.Add(CreateHistoryRow($"{record.DurationSeconds} сек", $"{record.PlayerDamageDealt}/{record.CompanionDamageDealt}", record.Winner));
                }
            }

            panel.Append(list);
            panel.Append(scrollbar);
            _contentPanel.Append(panel);
        }

        private UIElement CreateHistoryRow(string time, string damage, string winner)
        {
            UIPanel row = new UIPanel();
            row.Width.Set(0f, 1f);
            row.Height.Set(32f, 0f);
            row.BackgroundColor = new Color(28, 36, 68) * 0.62f;
            row.BorderColor = new Color(72, 96, 178) * 0.45f;
            row.SetPadding(0f);
            row.Append(CreateText(time, 8f, 7f, 0.70f));
            row.Append(CreateText(damage, 160f, 7f, 0.70f, new Color(255, 240, 170)));
            row.Append(CreateText(winner, 376f, 7f, 0.70f));
            return row;
        }

        private void BuildSettingsContent(AdaptiveDifficultyPlayer data)
        {
            _contentPanel.Append(CreateText("Настройка", 16f, 12f, 0.95f, new Color(255, 240, 170)));
            _contentPanel.Append(CreateText($"Навык {data.CurrentPowerSnapshot.PlayerSkillScore * 100f:0}% · Сила x{data.CurrentPowerSnapshot.EffectiveCoefficient:0.00} · {data.CurrentPowerProfile}", 156f, 18f, 0.74f, new Color(200, 230, 255)));

            UIPanel skillCard = CreateInfoPanel(24f, 58f, 360f, 150f, "Навык игрока");
            skillCard.Append(CreateText($"Активно: {data.CurrentPowerSnapshot.SkillSource}", 14f, 44f, 0.78f));
            skillCard.Append(CreateContentButton("Настоящий", 14f, 84f, 78f, 30f, () => SetSkillMode(CompanionSkillSimulationMode.Real), true, data.SkillSimulationMode == CompanionSkillSimulationMode.Real));
            skillCard.Append(CreateContentButton("Низкий", 102f, 84f, 70f, 30f, () => SetSkillMode(CompanionSkillSimulationMode.Low), true, data.SkillSimulationMode == CompanionSkillSimulationMode.Low));
            skillCard.Append(CreateContentButton("Средний", 182f, 84f, 76f, 30f, () => SetSkillMode(CompanionSkillSimulationMode.Medium), true, data.SkillSimulationMode == CompanionSkillSimulationMode.Medium));
            skillCard.Append(CreateContentButton("Высокий", 268f, 84f, 76f, 30f, () => SetSkillMode(CompanionSkillSimulationMode.High), true, data.SkillSimulationMode == CompanionSkillSimulationMode.High));
            _contentPanel.Append(skillCard);

            UIPanel scalarCard = CreateInfoPanel(410f, 58f, 360f, 150f, "Коэффициент");
            scalarCard.Append(CreateText($"x{data.ManualPowerScalar:0.0}", 152f, 44f, 1.14f, new Color(255, 240, 170)));
            scalarCard.Append(CreateContentButton("-0.5", 42f, 94f, 78f, 30f, () => ChangeManualPower(-0.5f)));
            scalarCard.Append(CreateContentButton("сброс", 142f, 94f, 78f, 30f, ResetManualPower));
            scalarCard.Append(CreateContentButton("+0.5", 242f, 94f, 78f, 30f, () => ChangeManualPower(0.5f)));
            _contentPanel.Append(scalarCard);

            UIPanel styleCard = CreateInfoPanel(24f, 236f, 360f, 278f, "Стиль боя");
            styleCard.Append(CreateText($"{data.CurrentCompanionStyle} · {data.CurrentPowerSnapshot.StyleSource}", 14f, 44f, 0.78f));
            styleCard.Append(CreateContentButton(data.ManualCombatStyleEnabled ? "Ручной" : "Авто", 234f, 38f, 94f, 30f, () =>
            {
                data.SetManualCombatStyle(!data.ManualCombatStyleEnabled, data.ManualCombatStyle, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }, true, data.ManualCombatStyleEnabled));
            BuildStyleButtons(styleCard, data);
            _contentPanel.Append(styleCard);

            UIPanel profileCard = CreateInfoPanel(410f, 236f, 360f, 278f, "Профиль силы");
            profileCard.Append(CreateText($"{data.CurrentPowerProfile} · {data.CurrentPowerSnapshot.ProfileSource}", 14f, 44f, 0.78f));
            profileCard.Append(CreateContentButton(data.ManualPowerProfileEnabled ? "Ручной" : "Авто", 234f, 38f, 94f, 30f, () =>
            {
                data.SetManualPowerProfile(!data.ManualPowerProfileEnabled, data.ManualPowerProfile, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }, true, data.ManualPowerProfileEnabled));
            BuildProfileButtons(profileCard, data);
            _contentPanel.Append(profileCard);
        }

        private void BuildStyleButtons(UIPanel panel, AdaptiveDifficultyPlayer data)
        {
            AddStyleButton(panel, data, CompanionCombatStyle.Balanced, "Баланс", 14f, 92f);
            AddStyleButton(panel, data, CompanionCombatStyle.Melee, "Ближний", 122f, 92f);
            AddStyleButton(panel, data, CompanionCombatStyle.Ranged, "Дальний", 230f, 92f);
            AddStyleButton(panel, data, CompanionCombatStyle.Magic, "Магия", 14f, 140f);
            AddStyleButton(panel, data, CompanionCombatStyle.Summon, "Призыв", 122f, 140f);
        }

        private void AddStyleButton(UIPanel panel, AdaptiveDifficultyPlayer data, CompanionCombatStyle style, string label, float left, float top)
        {
            panel.Append(CreateContentButton(label, left, top, 92f, 32f, () =>
            {
                data.SetManualCombatStyle(true, style, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }, data.ManualCombatStyleEnabled, data.ManualCombatStyleEnabled && data.ManualCombatStyle == style));
        }

        private void BuildProfileButtons(UIPanel panel, AdaptiveDifficultyPlayer data)
        {
            AddProfileButton(panel, data, CompanionPowerProfile.Weak, "Ослабл.", 14f, 92f);
            AddProfileButton(panel, data, CompanionPowerProfile.Balanced, "Баланс", 122f, 92f);
            AddProfileButton(panel, data, CompanionPowerProfile.Support, "Поддержка", 230f, 92f);
            AddProfileButton(panel, data, CompanionPowerProfile.Elite, "Элитный", 14f, 140f);
        }

        private void AddProfileButton(UIPanel panel, AdaptiveDifficultyPlayer data, CompanionPowerProfile profile, string label, float left, float top)
        {
            panel.Append(CreateContentButton(label, left, top, 92f, 32f, () =>
            {
                data.SetManualPowerProfile(true, profile, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }, data.ManualPowerProfileEnabled, data.ManualPowerProfileEnabled && data.ManualPowerProfile == profile));
        }

        private UIPanel CreateInfoPanel(float left, float top, float width, float height, string title)
        {
            UIPanel panel = new UIPanel();
            panel.Left.Set(left, 0f);
            panel.Top.Set(top, 0f);
            panel.Width.Set(width, 0f);
            panel.Height.Set(height, 0f);
            panel.BackgroundColor = new Color(33, 43, 79) * 0.72f;
            panel.BorderColor = new Color(95, 125, 225) * 0.82f;
            panel.Append(CreateText(title, 12f, 10f, 0.86f, new Color(255, 240, 170)));
            return panel;
        }

        private UIPanel CreateProgressBar(string title, int current, int maximum, float ratio, float left, float top, float width, Color fillColor)
        {
            ratio = MathHelper.Clamp(ratio, 0f, 1f);

            UIPanel wrapper = new UIPanel();
            wrapper.Left.Set(left, 0f);
            wrapper.Top.Set(top, 0f);
            wrapper.Width.Set(width, 0f);
            wrapper.Height.Set(52f, 0f);
            wrapper.BackgroundColor = new Color(33, 43, 79) * 0.65f;
            wrapper.BorderColor = new Color(95, 125, 225) * 0.65f;
            wrapper.SetPadding(0f);

            wrapper.Append(CreateText($"{title}: {current}/{maximum}", 10f, 6f, 0.76f));

            UIPanel track = new UIPanel();
            track.Left.Set(10f, 0f);
            track.Top.Set(28f, 0f);
            track.Width.Set(width - 20f, 0f);
            track.Height.Set(14f, 0f);
            track.BackgroundColor = new Color(16, 20, 38) * 0.9f;
            track.BorderColor = new Color(62, 82, 150) * 0.8f;
            track.SetPadding(0f);

            UIPanel fill = new UIPanel();
            fill.Left.Set(0f, 0f);
            fill.Top.Set(0f, 0f);
            fill.Width.Set((width - 20f) * ratio, 0f);
            fill.Height.Set(14f, 0f);
            fill.BackgroundColor = fillColor * 0.95f;
            fill.BorderColor = fillColor * 0.95f;
            fill.SetPadding(0f);
            track.Append(fill);

            wrapper.Append(track);
            return wrapper;
        }

        private void ChangeManualPower(float delta)
        {
            if (TryGetBoundCompanion(out _, out _, out _, out AdaptiveDifficultyPlayer data))
            {
                data.AdjustManualScalar(delta, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }
        }

        private void ResetManualPower()
        {
            if (TryGetBoundCompanion(out _, out _, out _, out AdaptiveDifficultyPlayer data))
            {
                data.SetManualScalar(1f, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }
        }

        private void SetSkillMode(CompanionSkillSimulationMode mode)
        {
            if (TryGetBoundCompanion(out _, out _, out _, out AdaptiveDifficultyPlayer data))
            {
                data.SetSkillSimulationMode(mode, Main.netMode == NetmodeID.MultiplayerClient);
                _needsRebuild = true;
            }
        }

        private void Close()
        {
            AdaptiveCompanionSystem.CloseInterface();
            Main.playerInventory = false;
        }

        private bool TryGetBoundCompanion(out NPC npc, out AdaptiveCompanionNPC companion, out Player owner, out AdaptiveDifficultyPlayer data)
        {
            npc = null;
            companion = null;
            owner = Main.LocalPlayer;
            data = null;

            if (_npcIndex < 0 || _npcIndex >= Main.maxNPCs)
            {
                return false;
            }

            npc = Main.npc[_npcIndex];
            if (!npc.active || npc.ModNPC is not AdaptiveCompanionNPC modNpc)
            {
                return false;
            }

            if (modNpc.OwnerPlayerIndex != owner.whoAmI)
            {
                return false;
            }

            companion = modNpc;
            data = owner.GetModPlayer<AdaptiveDifficultyPlayer>();
            return data != null;
        }
    }
}
