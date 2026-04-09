using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Data;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterUnitHUD : PanelContainer
    {
        private PlayerStats _targetStats;
        private Player _targetPlayer;

        [Export] public TextureProgressBar HealthBar;
        [Export] public TextureProgressBar ManaBar;
        [Export] public TextureProgressBar StaminaBar;
        [Export] public Label NameLabel;
        [Export] public TextureRect Portrait;
        
        private TextureRect frameBackground;
        private ShaderMaterial shaderMaterial;
        private const string ShaderPath = "res://assets/shader/outline.gdshader";
        private Control _activeSkillStrip;
        private readonly List<SkillBadgeView> _skillBadgeViews = new();

        private sealed class SkillBadgeView
        {
            public SkillData Skill;
            public PanelContainer Holder;
            public TextureRect Icon;
            public ColorRect Overlay;
            public ColorRect OverlayEdge;
        }

        public override void _Ready()
        {
            if(Portrait == null)
            {
                Portrait = GetNode<TextureRect>("TextureRect/Portrait");
            }

            // Lấy TextureRect đã có trong scene (background frame)
            frameBackground = GetNode<TextureRect>("TextureRect");
            
            // Load shader và áp dụng vào frameBackground
            var shader = GD.Load<Shader>(ShaderPath);
            if (shader != null && frameBackground != null)
            {
                shaderMaterial = new ShaderMaterial();
                shaderMaterial.Shader = shader;
                frameBackground.Material = shaderMaterial;
                
                shaderMaterial.SetShaderParameter("line_thickness", 0.0f);
                shaderMaterial.SetShaderParameter("line_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));                
                GD.Print("[CharacterUnitHUD] Shader applied to frameBackground");
            }
            else
            {
                GD.PrintErr($"[CharacterUnitHUD] Shader or frameBackground not found");
            }

            SetupActiveSkillStrip();
            if (frameBackground != null)
            {
                frameBackground.Resized += OnFrameBackgroundResized;
            }
            UpdateActiveSkillStripPlacement();
        }

        public override void _Process(double delta)
        {
            UpdateActiveSkillStripPlacement();

            if (!Visible || _targetStats == null)
            {
                return;
            }

            UpdateSkillOverlayState();
        }

        public void SetTarget(PlayerStats stats)
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;

            _targetStats = stats;
            _targetPlayer = null;
            if (_targetStats != null)
            {
                _targetStats.StatsChanged += UpdateUI;

                if (stats.ConfigData != null)
                {
                    if (NameLabel != null)
                        NameLabel.Text = stats.ConfigData.Name;
                    
                    if (Portrait != null && stats.ConfigData.Icon != null)
                        Portrait.Texture = stats.ConfigData.Icon;
                }

                RebuildActiveSkillStrip();
                UpdateUI();
            }
            else
            {
                RebuildActiveSkillStrip();
            }
        }

        private void UpdateUI()
        {
            if (_targetStats == null) return;

            if (HealthBar != null)
            {
                HealthBar.MaxValue = _targetStats.MaxHP; 
                HealthBar.Value = _targetStats.CurrentHP;
            }

            if (ManaBar != null)
            {
                ManaBar.MaxValue = _targetStats.MaxMP;
                ManaBar.Value = _targetStats.CurrentMP;
            }

            if (StaminaBar != null)
            {
                StaminaBar.MaxValue = _targetStats.MaxStamina; 
                StaminaBar.Value = _targetStats.CurrentStamina;
            }

            UpdateSkillOverlayState();
        }

        public void ApplyHighlight(bool isSelected)
        {
            if (shaderMaterial != null)
            {
                shaderMaterial.SetShaderParameter("line_thickness", isSelected ? 20.0f : 0.0f);
                GD.Print($"[CharacterUnitHUD] Highlight {(isSelected ? "ON" : "OFF")}");
            }
        }
        
        public override void _ExitTree()
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;

            if (frameBackground != null)
                frameBackground.Resized -= OnFrameBackgroundResized;
        }

        private void SetupActiveSkillStrip()
        {
            if (_activeSkillStrip != null)
            {
                return;
            }

            _activeSkillStrip = new Control();
            _activeSkillStrip.Name = "ActiveSkillStrip";
            _activeSkillStrip.Visible = false;
            _activeSkillStrip.MouseFilter = MouseFilterEnum.Ignore;
            _activeSkillStrip.SetAnchorsPreset(LayoutPreset.TopLeft);
            AddChild(_activeSkillStrip);
        }

        private void RebuildActiveSkillStrip()
        {
            SetupActiveSkillStrip();

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

                var badge = new PanelContainer();
                badge.Visible = false;
                badge.CustomMinimumSize = new Vector2(30, 24);
                badge.MouseFilter = MouseFilterEnum.Ignore;
                badge.SetAnchorsPreset(LayoutPreset.TopLeft);
                badge.Size = new Vector2(30.0f, 24.0f);

                var badgeStyle = new StyleBoxFlat();
                badgeStyle.BgColor = new Color(0.03f, 0.05f, 0.08f, 0.96f);
                badgeStyle.BorderColor = new Color(0.67f, 0.78f, 0.89f, 0.55f);
                badgeStyle.SetBorderWidthAll(1);
                badgeStyle.SetCornerRadiusAll(3);
                badge.AddThemeStyleboxOverride("panel", badgeStyle);
                _activeSkillStrip.AddChild(badge);

                var clipRoot = new Control();
                clipRoot.SetAnchorsPreset(LayoutPreset.FullRect);
                clipRoot.MouseFilter = MouseFilterEnum.Ignore;
                clipRoot.ClipContents = true;
                badge.AddChild(clipRoot);

                var iconCenter = new CenterContainer();
                iconCenter.SetAnchorsPreset(LayoutPreset.FullRect);
                iconCenter.OffsetLeft = 1.0f;
                iconCenter.OffsetTop = 1.0f;
                iconCenter.OffsetRight = -1.0f;
                iconCenter.OffsetBottom = -1.0f;
                iconCenter.MouseFilter = MouseFilterEnum.Ignore;
                clipRoot.AddChild(iconCenter);

                var icon = CreateAutoSizedSkillIcon(skill.Icon, 21.0f, 18.0f);
                iconCenter.AddChild(icon);

                var overlay = new ColorRect();
                overlay.MouseFilter = MouseFilterEnum.Ignore;
                overlay.Color = new Color(0.01f, 0.03f, 0.08f, 0.82f);
                clipRoot.AddChild(overlay);

                var overlayEdge = new ColorRect();
                overlayEdge.MouseFilter = MouseFilterEnum.Ignore;
                overlayEdge.Color = new Color(0.55f, 0.81f, 1.0f, 0.95f);
                clipRoot.AddChild(overlayEdge);

                badge.TooltipText = string.IsNullOrWhiteSpace(skill.SkillName) ? "Skill" : skill.SkillName;
                _skillBadgeViews.Add(new SkillBadgeView
                {
                    Skill = skill,
                    Holder = badge,
                    Icon = icon,
                    Overlay = overlay,
                    OverlayEdge = overlayEdge
                });
            }
        }

        private void UpdateSkillOverlayState()
        {
            if (_activeSkillStrip == null || _skillBadgeViews.Count == 0)
            {
                return;
            }

            _targetPlayer ??= ResolvePlayerForStats(_targetStats);

            SkillData activeSkill = _targetPlayer?.GetActiveTimedSkill();
            float duration = _targetPlayer != null ? Mathf.Max(0.01f, _targetPlayer.GetActiveTimedSkillDuration()) : 0.0f;
            float remaining = _targetPlayer != null ? _targetPlayer.GetActiveTimedSkillRemaining() : 0.0f;
            float overlayRatio = (activeSkill != null && remaining > 0.0f && duration > 0.0f)
                ? Mathf.Clamp(1.0f - (remaining / duration), 0.0f, 1.0f)
                : 0.0f;
            bool hasVisibleBadge = false;
            int visibleIndex = 0;

            foreach (var badgeView in _skillBadgeViews)
            {
                if (badgeView?.Holder == null || badgeView.Overlay == null || badgeView.OverlayEdge == null)
                {
                    continue;
                }

                bool isVisible = badgeView.Skill == activeSkill && overlayRatio > 0.0f;
                badgeView.Holder.Visible = isVisible;
                if (!isVisible)
                {
                    badgeView.Overlay.Visible = false;
                    badgeView.OverlayEdge.Visible = false;
                    continue;
                }

                hasVisibleBadge = true;
                badgeView.Holder.Position = new Vector2(visibleIndex * 34.0f, 0.0f);
                visibleIndex++;

                Vector2 badgeSize = badgeView.Overlay.GetParent<Control>().Size;
                float overlayHeight = badgeSize.Y * overlayRatio;

                badgeView.Overlay.Visible = overlayHeight > 0.5f;
                badgeView.Overlay.Position = Vector2.Zero;
                badgeView.Overlay.Size = new Vector2(badgeSize.X, overlayHeight);

                bool showEdge = overlayRatio > 0.0f && overlayRatio < 1.0f;
                badgeView.OverlayEdge.Visible = showEdge;
                if (showEdge)
                {
                    float edgeY = Mathf.Clamp(overlayHeight - 1.0f, 0.0f, Mathf.Max(0.0f, badgeSize.Y - 2.0f));
                    badgeView.OverlayEdge.Position = new Vector2(0.0f, edgeY);
                    badgeView.OverlayEdge.Size = new Vector2(badgeSize.X, 2.0f);
                }
            }

            _activeSkillStrip.Visible = hasVisibleBadge;
        }

        private void UpdateActiveSkillStripPlacement()
        {
            if (_activeSkillStrip == null)
            {
                return;
            }

            float top = frameBackground != null ? frameBackground.Size.Y - 2.0f : 98.0f;
            _activeSkillStrip.OffsetLeft = 8.0f;
            _activeSkillStrip.OffsetTop = top;
            _activeSkillStrip.OffsetRight = 180.0f;
            _activeSkillStrip.OffsetBottom = top + 26.0f;
        }

        private void OnFrameBackgroundResized()
        {
            UpdateActiveSkillStripPlacement();
        }

        private Player ResolvePlayerForStats(PlayerStats stats)
        {
            if (stats == null || GetTree() == null)
            {
                return null;
            }

            foreach (Node node in GetTree().GetNodesInGroup("Player"))
            {
                if (node is Player player && player.GetStatsNode() == stats)
                {
                    return player;
                }
            }

            return null;
        }

        private TextureRect CreateAutoSizedSkillIcon(Texture2D texture, float maxWidth, float maxHeight)
        {
            var iconRect = new TextureRect();
            iconRect.MouseFilter = MouseFilterEnum.Ignore;
            iconRect.Texture = texture;
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.CustomMinimumSize = ComputeSafeSkillIconSize(texture, maxWidth, maxHeight);
            return iconRect;
        }

        private Vector2 ComputeSafeSkillIconSize(Texture2D texture, float maxWidth, float maxHeight)
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
            float fittedWidth = Mathf.Max(10.0f, Mathf.Round(sourceSize.X * scale));
            float fittedHeight = Mathf.Max(10.0f, Mathf.Round(sourceSize.Y * scale));
            return new Vector2(fittedWidth, fittedHeight);
        }
    }
}
