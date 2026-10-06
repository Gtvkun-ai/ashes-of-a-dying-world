using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.UI.HUD.Skills;
using AshesofaDyingWorld.UI.Theme;
using System;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterUnitHUD : PanelContainer
    {
        public event Action<PlayerStats> ContextMenuRequested;

        [Export] public ProgressBar HealthBar;
        [Export] public ProgressBar ManaBar;
        [Export] public ProgressBar StaminaBar;
        [Export] public Label NameLabel;
        [Export] public Label LevelLabel;
        [Export] public TextureRect Portrait;

        private const string StatusEffectFrameTexturePath = "res://assets/graphics/ui/hud/status_effects/status_effect_icon_frame.png";
        private const string ChillStatusIconPath = "res://assets/graphics/ui/hud/status_effects/icons/chill.png";
        private const string SlowStatusIconPath = "res://assets/graphics/ui/hud/status_effects/icons/slow.png";
        private const string FrozenStatusIconPath = "res://assets/graphics/ui/hud/status_effects/icons/frozen.png";
        private const float StatusEffectBadgeSize = UiTokens.StatusFrameSize;
        private const float StatusEffectIconInset = 4.0f;
        private const float ActiveSkillBadgeSize = UiTokens.IconSizeMedium;
        private const float ActiveSkillIconInset = 2.0f;

        private PlayerStats _targetStats;
        private CombatCharacter _targetCombatant;
        private HBoxContainer _activeSkillStrip;
        private HBoxContainer _combatStatusStrip;
        private Texture2D _statusEffectFrameTexture;
        private Texture2D _chillStatusIconTexture;
        private Texture2D _slowStatusIconTexture;
        private Texture2D _frozenStatusIconTexture;
        private CombatStatusBadgeView _chillStatusBadge;
        private CombatStatusBadgeView _slowStatusBadge;
        private CombatStatusBadgeView _frozenStatusBadge;
        private readonly List<SkillBadgeView> _skillBadgeViews = new();

        public PlayerStats TargetStats => _targetStats;

        private sealed class SkillBadgeView
        {
            public SkillData Skill;
            public Control Holder;
            public ColorRect Overlay;
            public ColorRect OverlayEdge;
            public Label CooldownLabel;
        }

        private sealed class CombatStatusBadgeView
        {
            public Control Holder;
            public Label StackLabel;
        }

        public override void _Ready()
        {
            UiThemeFactory.Apply(this);
            ResolveNamedNodes();
            ConfigureHeader();
            ConfigureResourceBars();
            LoadStatusEffectTextures();
            SetupActiveSkillStrip();
            SetupCombatStatusStrip();
            FocusMode = Control.FocusModeEnum.All;
            MouseFilter = Control.MouseFilterEnum.Stop;
            ApplyHighlight(false);
        }

        private void ResolveNamedNodes()
        {
            HealthBar ??= GetNodeOrNull<ProgressBar>("Content/Columns/StatsColumn/ResourceRows/HealthRow/HealthBar");
            ManaBar ??= GetNodeOrNull<ProgressBar>("Content/Columns/StatsColumn/ResourceRows/ManaRow/ManaBar");
            StaminaBar ??= GetNodeOrNull<ProgressBar>("Content/Columns/StatsColumn/ResourceRows/StaminaRow/StaminaBar");
            NameLabel ??= GetNodeOrNull<Label>("Content/Columns/StatsColumn/HeaderRow/NameLabel");
            LevelLabel ??= GetNodeOrNull<Label>("Content/Columns/StatsColumn/HeaderRow/LevelLabel");
            Portrait ??= GetNodeOrNull<TextureRect>("Content/Columns/PortraitColumn/PortraitFrame/Portrait");

            if (HealthBar == null || ManaBar == null || StaminaBar == null || NameLabel == null || LevelLabel == null || Portrait == null)
            {
                GD.PrintErr("[CharacterUnitHUD] One or more exported HUD nodes could not be resolved.");
            }
        }

        private void ConfigureHeader()
        {
            if (NameLabel == null)
            {
                return;
            }

            NameLabel.Visible = true;
            NameLabel.MouseFilter = Control.MouseFilterEnum.Pass;
            NameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            NameLabel.TooltipText = NameLabel.Text;
            UiThemeFactory.ApplyText(NameLabel, UiTextRole.Label);

            if (LevelLabel != null)
            {
                LevelLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
                UiThemeFactory.ApplyText(LevelLabel, UiTextRole.Micro);
            }
        }

        private void ConfigureResourceBars()
        {
            ConfigureResourceBar(HealthBar, UiTokens.Life);
            ConfigureResourceBar(ManaBar, UiTokens.Memory);
            ConfigureResourceBar(StaminaBar, UiTokens.Accent);
        }

        private static void ConfigureResourceBar(ProgressBar bar, Color semanticColor)
        {
            if (bar == null)
            {
                return;
            }

            bar.ShowPercentage = false;
            bar.MouseFilter = Control.MouseFilterEnum.Ignore;
            bar.AddThemeStyleboxOverride("background", CreateBarStyle(UiTokens.Canvas));
            bar.AddThemeStyleboxOverride("fill", CreateBarStyle(semanticColor));
        }

        private static StyleBoxFlat CreateBarStyle(Color color)
        {
            var style = new StyleBoxFlat { BgColor = color };
            style.SetCornerRadiusAll(1);
            return style;
        }

        public override void _Process(double delta)
        {
            if (!Visible || _targetStats == null)
            {
                return;
            }

            UpdateSkillOverlayState();
            UpdateCombatStatusState();
        }

        public void SetTarget(PlayerStats stats)
        {
            if (_targetStats != null)
            {
                _targetStats.StatsChanged -= UpdateUI;
            }

            _targetStats = stats;
            _targetCombatant = null;

            if (_targetStats == null)
            {
                if (NameLabel != null)
                {
                    NameLabel.Text = "Không rõ";
                    NameLabel.TooltipText = "Không rõ";
                }

                if (LevelLabel != null)
                {
                    LevelLabel.Text = "Cấp --";
                }

                RebuildActiveSkillStrip();
                UpdateCombatStatusState();
                return;
            }

            _targetStats.StatsChanged += UpdateUI;
            if (stats.ConfigData != null)
            {
                string displayName = string.IsNullOrWhiteSpace(stats.ConfigData.Name)
                    ? "Không rõ"
                    : stats.ConfigData.Name;

                if (NameLabel != null)
                {
                    NameLabel.Text = displayName;
                    NameLabel.TooltipText = displayName;
                }

                if (Portrait != null && stats.ConfigData.Icon != null)
                {
                    Portrait.Texture = stats.ConfigData.Icon;
                }
            }

            RebuildActiveSkillStrip();
            UpdateCombatStatusState();
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_targetStats == null)
            {
                return;
            }

            SetResourceValue(HealthBar, _targetStats.CurrentHP, _targetStats.MaxHP, UiTokens.Life);
            SetResourceValue(ManaBar, _targetStats.CurrentMP, _targetStats.MaxMP, UiTokens.Memory);
            SetResourceValue(StaminaBar, _targetStats.CurrentStamina, _targetStats.MaxStamina, UiTokens.Accent);
            if (LevelLabel != null)
            {
                LevelLabel.Text = $"Cấp {_targetStats.CurrentLevel:00}";
            }
            UpdateSkillOverlayState();
            UpdateCombatStatusState();
        }

        private static void SetResourceValue(ProgressBar bar, float value, float maximum, Color semanticColor)
        {
            if (bar == null)
            {
                return;
            }

            bar.MaxValue = Mathf.Max(1.0f, maximum);
            bar.Value = Mathf.Clamp(value, 0.0f, bar.MaxValue);
            Color stateColor = bar.Value / bar.MaxValue <= 0.25f ? UiTokens.Danger : semanticColor;
            bar.AddThemeStyleboxOverride("fill", CreateBarStyle(stateColor));
        }

        public void ApplyHighlight(bool isSelected)
        {
            AddThemeStyleboxOverride("panel", CreatePanelStyle(isSelected));
        }

        private static StyleBoxFlat CreatePanelStyle(bool selected)
        {
            var style = new StyleBoxFlat
            {
                BgColor = UiTokens.Surface,
                BorderColor = selected ? UiTokens.Accent : UiTokens.Border,
                ContentMarginLeft = 0,
                ContentMarginTop = 0,
                ContentMarginRight = 0,
                ContentMarginBottom = 0
            };
            style.SetBorderWidthAll(selected ? UiTokens.SelectionBorderWidth : UiTokens.BorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);
            return style;
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
            {
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    GrabFocus();
                }

                if (mouse.ButtonIndex == MouseButton.Right && _targetStats != null)
                {
                    RequestContextMenu();
                }

                return;
            }

            if (_targetStats != null && inputEvent.IsActionPressed("ui_accept"))
            {
                RequestContextMenu();
            }
        }

        private void RequestContextMenu()
        {
            ContextMenuRequested?.Invoke(_targetStats);
            AcceptEvent();
        }

        private void LoadStatusEffectTextures()
        {
            _statusEffectFrameTexture = GD.Load<Texture2D>(StatusEffectFrameTexturePath);
            _chillStatusIconTexture = GD.Load<Texture2D>(ChillStatusIconPath);
            _slowStatusIconTexture = GD.Load<Texture2D>(SlowStatusIconPath);
            _frozenStatusIconTexture = GD.Load<Texture2D>(FrozenStatusIconPath);
        }

        private void SetupCombatStatusStrip()
        {
            _combatStatusStrip = GetNodeOrNull<HBoxContainer>("Content/Columns/PortraitColumn/StatusFrame/StatusStrip");
            if (_combatStatusStrip == null)
            {
                GD.PrintErr("[CharacterUnitHUD] StatusStrip is missing from the unit scene.");
                return;
            }

            _combatStatusStrip.MouseFilter = Control.MouseFilterEnum.Pass;
            _combatStatusStrip.TooltipText = "Trạng thái";
            _chillStatusBadge = CreateCombatStatusBadge(_chillStatusIconTexture, true, "Nhiễm lạnh");
            _slowStatusBadge = CreateCombatStatusBadge(_slowStatusIconTexture, false, "Chậm");
            _frozenStatusBadge = CreateCombatStatusBadge(_frozenStatusIconTexture, false, "Đóng băng");
        }

        private CombatStatusBadgeView CreateCombatStatusBadge(Texture2D iconTexture, bool showStack, string tooltip)
        {
            var holder = new Control
            {
                Name = tooltip + "Status",
                Visible = false,
                CustomMinimumSize = new Vector2(StatusEffectBadgeSize, StatusEffectBadgeSize),
                MouseFilter = Control.MouseFilterEnum.Pass,
                TooltipText = tooltip
            };
            _combatStatusStrip.AddChild(holder);

            var background = new ColorRect
            {
                Color = UiTokens.Canvas,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            SetFullRectInsets(background, StatusEffectIconInset);
            holder.AddChild(background);

            var icon = new TextureRect
            {
                Texture = iconTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            SetFullRectInsets(icon, StatusEffectIconInset - 1f);
            holder.AddChild(icon);

            if (_statusEffectFrameTexture != null)
            {
                var frame = new TextureRect
                {
                    Texture = _statusEffectFrameTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                holder.AddChild(frame);
            }

            Label stackLabel = null;
            if (showStack)
            {
                stackLabel = new Label
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false
                };
                stackLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                stackLabel.AddThemeFontSizeOverride("font_size", UiTokens.MicroFontSize);
                stackLabel.AddThemeColorOverride("font_color", UiTokens.TextPrimary);
                stackLabel.AddThemeColorOverride("font_outline_color", UiTokens.Canvas);
                stackLabel.AddThemeConstantOverride("outline_size", 2);
                holder.AddChild(stackLabel);
            }

            return new CombatStatusBadgeView { Holder = holder, StackLabel = stackLabel };
        }

        private void UpdateCombatStatusState()
        {
            if (_combatStatusStrip == null)
            {
                return;
            }

            _targetCombatant ??= ResolveCombatantForStats(_targetStats);
            CombatStatusController statuses = _targetCombatant?.Statuses;
            bool frozen = statuses?.IsFrozen == true;
            bool chill = !frozen && statuses?.HasChill == true;
            bool slow = !frozen && statuses?.IsSlowed == true;

            SetCombatStatusBadgeVisible(_chillStatusBadge, chill);
            SetCombatStatusBadgeVisible(_slowStatusBadge, slow);
            SetCombatStatusBadgeVisible(_frozenStatusBadge, frozen);

            if (_chillStatusBadge?.StackLabel != null)
            {
                int stacks = statuses?.ChillStacks ?? 0;
                _chillStatusBadge.StackLabel.Text = stacks > 1 ? stacks.ToString() : string.Empty;
                _chillStatusBadge.StackLabel.Visible = chill && stacks > 1;
            }
        }

        private static void SetCombatStatusBadgeVisible(CombatStatusBadgeView badge, bool visible)
        {
            if (badge?.Holder != null)
            {
                badge.Holder.Visible = visible;
            }
        }

        private void SetupActiveSkillStrip()
        {
            _activeSkillStrip = GetNodeOrNull<HBoxContainer>("Content/Columns/StatsColumn/HeaderRow/ActiveSkillStrip");
            if (_activeSkillStrip == null)
            {
                GD.PrintErr("[CharacterUnitHUD] ActiveSkillStrip is missing from the unit scene.");
                return;
            }

            _activeSkillStrip.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        private void RebuildActiveSkillStrip()
        {
            if (_activeSkillStrip == null)
            {
                return;
            }

            foreach (Node child in _activeSkillStrip.GetChildren())
            {
                child.QueueFree();
            }

            _skillBadgeViews.Clear();
            var skills = _targetStats?.ConfigData?.ActiveSkills;
            if (skills == null || skills.Count == 0)
            {
                _activeSkillStrip.Visible = false;
                return;
            }

            foreach (var skill in skills)
            {
                if (skill == null)
                {
                    continue;
                }

                var badge = new Control
                {
                    Visible = false,
                    CustomMinimumSize = new Vector2(ActiveSkillBadgeSize, ActiveSkillBadgeSize),
                    MouseFilter = Control.MouseFilterEnum.Pass,
                    TooltipText = string.IsNullOrWhiteSpace(skill.SkillName) ? "Kỹ năng" : skill.SkillName
                };
                _activeSkillStrip.AddChild(badge);

                var clipRoot = new Control { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
                SetFullRectInsets(clipRoot, ActiveSkillIconInset);
                badge.AddChild(clipRoot);

                var iconBackground = new ColorRect { Color = UiTokens.Canvas, MouseFilter = Control.MouseFilterEnum.Ignore };
                iconBackground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                clipRoot.AddChild(iconBackground);

                var iconCenter = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                iconCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                clipRoot.AddChild(iconCenter);
                iconCenter.AddChild(CreateAutoSizedSkillIcon(
                    SkillIconResolver.Resolve(skill),
                    16.0f,
                    16.0f));

                var overlay = new ColorRect
                {
                    Color = new Color(0.02f, 0.01f, 0.00f, 0.58f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false
                };
                clipRoot.AddChild(overlay);

                var overlayEdge = new ColorRect
                {
                    Color = UiTokens.Accent,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false
                };
                clipRoot.AddChild(overlayEdge);

                if (_statusEffectFrameTexture != null)
                {
                    var frame = new TextureRect
                    {
                        Texture = _statusEffectFrameTexture,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.Scale,
                        MouseFilter = Control.MouseFilterEnum.Ignore
                    };
                    frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                    badge.AddChild(frame);
                }

                var cooldownLabel = new Label
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                cooldownLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                cooldownLabel.AddThemeFontSizeOverride("font_size", UiTokens.MicroFontSize);
                cooldownLabel.AddThemeColorOverride("font_color", UiTokens.TextPrimary);
                cooldownLabel.AddThemeColorOverride("font_outline_color", UiTokens.Canvas);
                cooldownLabel.AddThemeConstantOverride("outline_size", 2);
                badge.AddChild(cooldownLabel);

                _skillBadgeViews.Add(new SkillBadgeView
                {
                    Skill = skill,
                    Holder = badge,
                    Overlay = overlay,
                    OverlayEdge = overlayEdge,
                    CooldownLabel = cooldownLabel
                });
            }
        }

        private void UpdateSkillOverlayState()
        {
            if (_activeSkillStrip == null || _skillBadgeViews.Count == 0)
            {
                return;
            }

            _targetCombatant ??= ResolveCombatantForStats(_targetStats);
            bool anyVisible = false;
            foreach (SkillBadgeView badgeView in _skillBadgeViews)
            {
                if (badgeView?.Holder == null || badgeView.Skill == null)
                {
                    continue;
                }

                badgeView.Holder.Visible = true;
                anyVisible = true;
                float remaining = _targetCombatant?.Abilities?.GetCooldownRemaining(badgeView.Skill) ?? 0.0f;
                float duration = Mathf.Max(0.01f, badgeView.Skill.Cooldown);
                float ratio = Mathf.Clamp(remaining / duration, 0.0f, 1.0f);
                Control clipRoot = badgeView.Overlay.GetParent<Control>();
                Vector2 innerSize = clipRoot?.Size ?? new Vector2(20.0f, 20.0f);
                float overlayHeight = innerSize.Y * ratio;

                badgeView.Overlay.Visible = remaining > 0.05f && overlayHeight > 0.5f;
                badgeView.Overlay.Position = new Vector2(0.0f, innerSize.Y - overlayHeight);
                badgeView.Overlay.Size = new Vector2(innerSize.X, overlayHeight);

                bool showEdge = remaining > 0.05f && ratio > 0.02f && ratio < 0.98f;
                badgeView.OverlayEdge.Visible = showEdge;
                if (showEdge)
                {
                    float edgeY = Mathf.Clamp(innerSize.Y - overlayHeight - 1.0f, 0.0f, Mathf.Max(0.0f, innerSize.Y - 2.0f));
                    badgeView.OverlayEdge.Position = new Vector2(0.0f, edgeY);
                    badgeView.OverlayEdge.Size = new Vector2(innerSize.X, 2.0f);
                }

                badgeView.CooldownLabel.Text = remaining > 0.05f
                    ? (remaining >= 10.0f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"))
                    : string.Empty;
            }

            _activeSkillStrip.Visible = anyVisible;
        }

        private CombatCharacter ResolveCombatantForStats(PlayerStats stats)
        {
            if (stats == null || GetTree() == null)
            {
                return null;
            }

            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is CombatCharacter combatant && combatant.Stats == stats)
                {
                    return combatant;
                }
            }

            return null;
        }

        private TextureRect CreateAutoSizedSkillIcon(Texture2D texture, float maxWidth, float maxHeight)
        {
            var iconRect = new TextureRect
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = ComputeSafeSkillIconSize(texture, maxWidth, maxHeight)
            };
            return iconRect;
        }

        private static Vector2 ComputeSafeSkillIconSize(Texture2D texture, float maxWidth, float maxHeight)
        {
            if (texture == null)
            {
                return new Vector2(maxWidth, maxHeight);
            }

            Vector2 sourceSize = texture.GetSize();
            if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f)
            {
                return new Vector2(maxWidth, maxHeight);
            }

            float scale = Mathf.Min(Mathf.Min(maxWidth / sourceSize.X, maxHeight / sourceSize.Y), 1.0f);
            return new Vector2(
                Mathf.Max(10.0f, Mathf.Round(sourceSize.X * scale)),
                Mathf.Max(10.0f, Mathf.Round(sourceSize.Y * scale)));
        }

        private static void SetFullRectInsets(Control control, float inset)
        {
            control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            control.OffsetLeft = inset;
            control.OffsetTop = inset;
            control.OffsetRight = -inset;
            control.OffsetBottom = -inset;
        }

        public override void _ExitTree()
        {
            if (_targetStats != null)
            {
                _targetStats.StatsChanged -= UpdateUI;
            }

        }
    }
}
