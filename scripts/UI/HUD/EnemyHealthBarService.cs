using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.UI.Theme;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class EnemyHealthBarService : CanvasLayer
    {
        private sealed class StatusBadge
        {
            public Control Holder;
            public TextureRect Icon;
            public Label StackLabel;
        }

        private class TrackedEnemy
        {
            public Node2D EnemyNode;
            public Func<float> GetCurrentHp;
            public Func<float> GetMaxHp;
            public Func<int> GetLevel;
            public Control Widget;
            public ProgressBar Bar;
            public PanelContainer HpFrame;
            public Label LevelLabel;
            public Control StatusRow;
            public StatusBadge ChillBadge;
            public StatusBadge SlowBadge;
            public StatusBadge FrozenBadge;
            public float LastHp;
            public float RevealRemaining;
        }

        public static EnemyHealthBarService Instance { get; private set; }

        private readonly List<TrackedEnemy> _tracked = new();

        [Export] public float WidgetScale = 1.0f;
        [Export] public Vector2 HpBarSize = new(40, 10);
        [Export] public float RowSpacing = 4f;
        [Export] public float LevelVerticalOffset = -1f;
        [Export] public float RevealSeconds = 3.2f;

        private const string StatusFrameTexturePath = "res://assets/graphics/ui/hud/status_effects/status_effect_icon_frame.png";
        private const string ChillIconPath = "res://assets/graphics/ui/hud/status_effects/icons/chill.png";
        private const string SlowIconPath = "res://assets/graphics/ui/hud/status_effects/icons/slow.png";
        private const string FrozenIconPath = "res://assets/graphics/ui/hud/status_effects/icons/frozen.png";
        private const float StatusBadgeSize = 16f;
        private const float StatusBadgeSpacing = 1f;

        private Texture2D _statusFrameTexture;
        private Texture2D _chillIconTexture;
        private Texture2D _slowIconTexture;
        private Texture2D _frozenIconTexture;
        private StyleBoxFlat _hpTroughStyle;
        private StyleBoxFlat _hpFillStyle;
        private StyleBoxFlat _hpFrameStyle;

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                GD.PrintErr("EnemyHealthBarService da ton tai. Chi nen co mot instance duy nhat.");
                QueueFree();
                return;
            }

            Instance = this;
            Layer = 55;
            EnsureDefaultTextures();
            BuildHpStyles();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static EnemyHealthBarService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }
            if (tree?.Root == null)
            {
                return null;
            }
            var service = new EnemyHealthBarService { Name = "EnemyHealthBarService" };
            tree.Root.AddChild(service);
            return service;
        }

        private void EnsureDefaultTextures()
        {
            _statusFrameTexture ??= GD.Load<Texture2D>(StatusFrameTexturePath);
            _chillIconTexture ??= GD.Load<Texture2D>(ChillIconPath);
            _slowIconTexture ??= GD.Load<Texture2D>(SlowIconPath);
            _frozenIconTexture ??= GD.Load<Texture2D>(FrozenIconPath);
        }

        public void RegisterEnemy(Node2D enemy, Func<float> getCurrentHp, Func<float> getMaxHp, Func<int> getLevel)
        {
            if (enemy == null || getCurrentHp == null || getMaxHp == null || getLevel == null)
            {
                return;
            }

            foreach (TrackedEnemy trackedEnemy in _tracked)
            {
                if (trackedEnemy.EnemyNode == enemy)
                {
                    return;
                }
            }

            EnsureDefaultTextures();

            var widget = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Scale = new Vector2(WidgetScale, WidgetScale),
                TopLevel = true,
                ZIndex = 100,
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            UiThemeFactory.Apply(widget);

            var levelLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            levelLabel.AddThemeFontSizeOverride("font_size", 13);
            levelLabel.AddThemeColorOverride("font_color", Colors.White);
            levelLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            levelLabel.AddThemeConstantOverride("outline_size", 3);
            widget.AddChild(levelLabel);

            var hpBar = new ProgressBar
            {
                CustomMinimumSize = HpBarSize,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            hpBar.AddThemeStyleboxOverride("background", _hpTroughStyle);
            hpBar.AddThemeStyleboxOverride("fill", _hpFillStyle);

            var hpFrame = new PanelContainer
            {
                Name = "HealthFrame",
                CustomMinimumSize = HpBarSize,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            hpFrame.AddThemeStyleboxOverride("panel", _hpFrameStyle);
            hpFrame.AddChild(hpBar);
            widget.AddChild(hpFrame);

            var statusRow = new Control
            {
                Name = "StatusRow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            widget.AddChild(statusRow);

            StatusBadge chillBadge = CreateStatusBadge(statusRow, _chillIconTexture, true, "Chill");
            StatusBadge slowBadge = CreateStatusBadge(statusRow, _slowIconTexture, false, "Slow");
            StatusBadge frozenBadge = CreateStatusBadge(statusRow, _frozenIconTexture, false, "Frozen");

            AddChild(widget);

            float currentHp = getCurrentHp();
            _tracked.Add(new TrackedEnemy
            {
                EnemyNode = enemy,
                GetCurrentHp = getCurrentHp,
                GetMaxHp = getMaxHp,
                GetLevel = getLevel,
                Widget = widget,
                Bar = hpBar,
                HpFrame = hpFrame,
                LevelLabel = levelLabel,
                StatusRow = statusRow,
                ChillBadge = chillBadge,
                SlowBadge = slowBadge,
                FrozenBadge = frozenBadge,
                LastHp = currentHp,
                RevealRemaining = 0f
            });
        }

        public void NotifyDamaged(Node2D enemy)
        {
            foreach (TrackedEnemy tracked in _tracked)
            {
                if (tracked.EnemyNode == enemy)
                {
                    tracked.RevealRemaining = Mathf.Max(tracked.RevealRemaining, RevealSeconds);
                    return;
                }
            }
        }

        public void NotifyTargeted(Node2D enemy)
        {
            foreach (TrackedEnemy tracked in _tracked)
            {
                if (tracked.EnemyNode == enemy)
                {
                    tracked.RevealRemaining = Mathf.Max(tracked.RevealRemaining, 0.4f);
                    return;
                }
            }
        }

        public void UnregisterEnemy(Node2D enemy)
        {
            for (int i = 0; i < _tracked.Count; i++)
            {
                if (_tracked[i].EnemyNode == enemy)
                {
                    _tracked[i].Widget.QueueFree();
                    _tracked.RemoveAt(i);
                    return;
                }
            }
        }

        public override void _Process(double delta)
        {
            Viewport viewport = GetViewport();
            if (viewport == null || viewport.GetCamera2D() == null)
            {
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                TrackedEnemy trackedEnemy = _tracked[i];
                if (!GodotObject.IsInstanceValid(trackedEnemy.EnemyNode))
                {
                    trackedEnemy.Widget.QueueFree();
                    _tracked.RemoveAt(i);
                    continue;
                }

                float maxHp = Math.Max(1f, trackedEnemy.GetMaxHp());
                float curHp = Math.Clamp(trackedEnemy.GetCurrentHp(), 0f, maxHp);
                if (curHp + 0.01f < trackedEnemy.LastHp)
                {
                    trackedEnemy.RevealRemaining = Mathf.Max(trackedEnemy.RevealRemaining, RevealSeconds);
                }
                trackedEnemy.LastHp = curHp;
                trackedEnemy.RevealRemaining = Mathf.Max(0f, trackedEnemy.RevealRemaining - dt);

                var statuses = (trackedEnemy.EnemyNode as CombatCharacter)?.Statuses;
                int visibleStatusCount = UpdateStatusBadges(trackedEnemy, statuses);
                if (visibleStatusCount > 0)
                {
                    trackedEnemy.RevealRemaining = Mathf.Max(trackedEnemy.RevealRemaining, 0.25f);
                }

                trackedEnemy.Bar.MaxValue = maxHp;
                trackedEnemy.Bar.Value = curHp;
                trackedEnemy.LevelLabel.Text = trackedEnemy.GetLevel().ToString();
                trackedEnemy.Widget.Scale = new Vector2(WidgetScale, WidgetScale);

                Vector2 levelSize = trackedEnemy.LevelLabel.GetCombinedMinimumSize();
                Vector2 barSize = trackedEnemy.HpFrame.GetCombinedMinimumSize();
                float baseHeight = Mathf.Max(levelSize.Y, barSize.Y);
                float rawLevelY = (baseHeight - levelSize.Y) * 0.5f + LevelVerticalOffset;
                float rawBarY = (baseHeight - barSize.Y) * 0.5f;
                float minY = Mathf.Min(rawLevelY, rawBarY);

                Vector2 levelPos = new(0f, rawLevelY - minY);
                Vector2 barPos = new(levelSize.X + RowSpacing, rawBarY - minY);
                float rowWidth = levelSize.X + RowSpacing + barSize.X;
                float rowHeight = Mathf.Max(levelPos.Y + levelSize.Y, barPos.Y + barSize.Y);

                trackedEnemy.LevelLabel.Position = levelPos;
                trackedEnemy.HpFrame.Position = barPos;
                if (trackedEnemy.StatusRow.Visible && visibleStatusCount > 0)
                {
                    float statusWidth = visibleStatusCount * StatusBadgeSize
                        + Mathf.Max(0, visibleStatusCount - 1) * StatusBadgeSpacing;
                    trackedEnemy.StatusRow.Position = new Vector2(
                        Mathf.Round((rowWidth - statusWidth) * 0.5f),
                        rowHeight + 1f);
                    trackedEnemy.StatusRow.Size = new Vector2(statusWidth, StatusBadgeSize);
                    rowHeight += StatusBadgeSize + 2f;
                }
                trackedEnemy.Widget.CustomMinimumSize = new Vector2(rowWidth, rowHeight);

                Vector2 widgetSize = trackedEnemy.Widget.GetCombinedMinimumSize() * WidgetScale;
                trackedEnemy.Widget.Position = WorldHudLayout.Resolve(
                    trackedEnemy.EnemyNode,
                    WorldHudLane.Health,
                    widgetSize,
                    new Vector2(0f, -6f));

                float targetAlpha = trackedEnemy.RevealRemaining > 0f ? 1f : 0f;
                float alpha = Mathf.MoveToward(trackedEnemy.Widget.Modulate.A, targetAlpha, dt * 5.5f);
                trackedEnemy.Widget.Modulate = new Color(1f, 1f, 1f, alpha);
            }
        }

        private void BuildHpStyles()
        {
            _hpTroughStyle = new StyleBoxFlat
            {
                BgColor = UiTokens.Canvas,
                BorderColor = UiTokens.Border
            };
            _hpTroughStyle.SetBorderWidthAll(UiTokens.BorderWidth);
            _hpTroughStyle.SetCornerRadiusAll(UiTokens.CornerRadius);

            _hpFillStyle = new StyleBoxFlat
            {
                BgColor = UiTokens.Life,
                BorderColor = UiTokens.TextPrimary
            };
            _hpFillStyle.SetBorderWidthAll(UiTokens.BorderWidth);
            _hpFillStyle.SetCornerRadiusAll(UiTokens.CornerRadius);

            _hpFrameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0f, 0f, 0f, 0f),
                BorderColor = UiTokens.BorderStrong
            };
            _hpFrameStyle.SetBorderWidthAll(UiTokens.SelectionBorderWidth);
            _hpFrameStyle.SetCornerRadiusAll(UiTokens.CornerRadius);
            _hpFrameStyle.ContentMarginLeft = 2f;
            _hpFrameStyle.ContentMarginTop = 2f;
            _hpFrameStyle.ContentMarginRight = 2f;
            _hpFrameStyle.ContentMarginBottom = 2f;
        }

        private StatusBadge CreateStatusBadge(
            Control parent,
            Texture2D iconTexture,
            bool showStack,
            string tooltip)
        {
            var holder = new Control
            {
                CustomMinimumSize = new Vector2(StatusBadgeSize, StatusBadgeSize),
                Size = new Vector2(StatusBadgeSize, StatusBadgeSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                TooltipText = tooltip
            };
            parent.AddChild(holder);

            var icon = new TextureRect
            {
                Texture = iconTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(2f, 2f),
                Size = new Vector2(StatusBadgeSize - 4f, StatusBadgeSize - 4f)
            };
            holder.AddChild(icon);

            if (_statusFrameTexture != null)
            {
                var frame = new TextureRect
                {
                    Texture = _statusFrameTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Size = new Vector2(StatusBadgeSize, StatusBadgeSize)
                };
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
                    Position = Vector2.Zero,
                    Size = new Vector2(StatusBadgeSize - 1f, StatusBadgeSize - 1f),
                    Visible = false
                };
                stackLabel.AddThemeFontSizeOverride("font_size", 8);
                stackLabel.AddThemeColorOverride("font_color", Colors.White);
                stackLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
                stackLabel.AddThemeConstantOverride("outline_size", 2);
                holder.AddChild(stackLabel);
            }

            return new StatusBadge
            {
                Holder = holder,
                Icon = icon,
                StackLabel = stackLabel
            };
        }

        private int UpdateStatusBadges(TrackedEnemy tracked, CombatStatusController statuses)
        {
            if (tracked?.StatusRow == null)
            {
                return 0;
            }

            bool frozen = statuses?.IsFrozen == true;
            bool chill = !frozen && statuses?.HasChill == true;
            bool slow = !frozen && statuses?.IsSlowed == true;

            int index = 0;
            SetBadgeVisible(tracked.ChillBadge, chill, ref index);
            SetBadgeVisible(tracked.SlowBadge, slow, ref index);
            SetBadgeVisible(tracked.FrozenBadge, frozen, ref index);

            if (tracked.ChillBadge?.StackLabel != null)
            {
                int stacks = statuses?.ChillStacks ?? 0;
                tracked.ChillBadge.StackLabel.Text = stacks > 1 ? stacks.ToString() : string.Empty;
                tracked.ChillBadge.StackLabel.Visible = chill && stacks > 1;
            }

            tracked.StatusRow.Visible = index > 0;
            return index;
        }

        private static void SetBadgeVisible(StatusBadge badge, bool visible, ref int index)
        {
            if (badge?.Holder == null)
            {
                return;
            }

            badge.Holder.Visible = visible;
            if (!visible)
            {
                return;
            }

            badge.Holder.Position = new Vector2(index * (StatusBadgeSize + StatusBadgeSpacing), 0f);
            index++;
        }

    }
}
