using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class EnemyHealthBarService : CanvasLayer
    {
        private class TrackedEnemy
        {
            public Node2D EnemyNode;
            public Func<float> GetCurrentHp;
            public Func<float> GetMaxHp;
            public Func<int> GetLevel;
            public Control Widget;
            public TextureProgressBar Bar;
            public Label LevelLabel;
            public Label StatusLabel;
            public float LastHp;
            public float RevealRemaining;
        }

        public static EnemyHealthBarService Instance { get; private set; }

        private readonly List<TrackedEnemy> _tracked = new();

        [Export] public Vector2 ScreenOffset = new(0, -30);
        [Export] public float WidgetScale = 1.0f;
        [Export] public Vector2 HpBarSize = new(40, 10);
        [Export] public float RowSpacing = 4f;
        [Export] public float LevelVerticalOffset = -1f;
        [Export] public float RevealSeconds = 3.2f;

        [Export] public Texture2D HpTextureUnder { get; set; }
        [Export] public Texture2D HpTextureProgress { get; set; }
        [Export] public Texture2D HpTextureOver { get; set; }

        private const string DefaultEnemyHpTexturePath = "res://assets/graphics/ui/status/enemy_hp_bar.png";

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
            if (HpTextureProgress == null)
            {
                HpTextureProgress = GD.Load<Texture2D>(DefaultEnemyHpTexturePath);
            }
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

            var levelLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            levelLabel.AddThemeFontSizeOverride("font_size", 13);
            levelLabel.AddThemeColorOverride("font_color", Colors.White);
            levelLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            levelLabel.AddThemeConstantOverride("outline_size", 3);
            widget.AddChild(levelLabel);

            var hpBar = new TextureProgressBar
            {
                CustomMinimumSize = HpBarSize,
                MaxValue = 100,
                Value = 100
            };
            if (HpTextureUnder != null) hpBar.TextureUnder = HpTextureUnder;
            if (HpTextureProgress != null) hpBar.TextureProgress = HpTextureProgress;
            if (HpTextureOver != null) hpBar.TextureOver = HpTextureOver;
            widget.AddChild(hpBar);

            var statusLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            statusLabel.AddThemeFontSizeOverride("font_size", 9);
            statusLabel.AddThemeColorOverride("font_color", new Color(0.58f, 0.90f, 1f));
            statusLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            statusLabel.AddThemeConstantOverride("outline_size", 2);
            widget.AddChild(statusLabel);
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
                LevelLabel = levelLabel,
                StatusLabel = statusLabel,
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

                string statusText = (trackedEnemy.EnemyNode as CombatCharacter)?.Statuses?.GetCompactLabel() ?? string.Empty;
                trackedEnemy.StatusLabel.Text = statusText;
                trackedEnemy.StatusLabel.Visible = !string.IsNullOrWhiteSpace(statusText);
                if (trackedEnemy.StatusLabel.Visible)
                {
                    trackedEnemy.RevealRemaining = Mathf.Max(trackedEnemy.RevealRemaining, 0.25f);
                }

                trackedEnemy.Bar.MaxValue = maxHp;
                trackedEnemy.Bar.Value = curHp;
                trackedEnemy.LevelLabel.Text = trackedEnemy.GetLevel().ToString();
                trackedEnemy.Widget.Scale = new Vector2(WidgetScale, WidgetScale);

                Vector2 levelSize = trackedEnemy.LevelLabel.GetCombinedMinimumSize();
                Vector2 barSize = trackedEnemy.Bar.GetCombinedMinimumSize();
                float baseHeight = Mathf.Max(levelSize.Y, barSize.Y);
                float rawLevelY = (baseHeight - levelSize.Y) * 0.5f + LevelVerticalOffset;
                float rawBarY = (baseHeight - barSize.Y) * 0.5f;
                float minY = Mathf.Min(rawLevelY, rawBarY);

                Vector2 levelPos = new(0f, rawLevelY - minY);
                Vector2 barPos = new(levelSize.X + RowSpacing, rawBarY - minY);
                float rowWidth = levelSize.X + RowSpacing + barSize.X;
                float rowHeight = Mathf.Max(levelPos.Y + levelSize.Y, barPos.Y + barSize.Y);

                trackedEnemy.LevelLabel.Position = levelPos;
                trackedEnemy.Bar.Position = barPos;
                if (trackedEnemy.StatusLabel.Visible)
                {
                    trackedEnemy.StatusLabel.Position = new Vector2(0f, rowHeight + 1f);
                    trackedEnemy.StatusLabel.Size = new Vector2(rowWidth, 12f);
                    rowHeight += 13f;
                }
                trackedEnemy.Widget.CustomMinimumSize = new Vector2(rowWidth, rowHeight);

                Vector2 screenPos = trackedEnemy.EnemyNode.GetGlobalTransformWithCanvas().Origin + ScreenOffset;
                Vector2 widgetSize = trackedEnemy.Widget.GetCombinedMinimumSize() * WidgetScale;
                trackedEnemy.Widget.Position = screenPos - new Vector2(widgetSize.X / 2f, widgetSize.Y);

                float targetAlpha = trackedEnemy.RevealRemaining > 0f ? 1f : 0f;
                float alpha = Mathf.MoveToward(trackedEnemy.Widget.Modulate.A, targetAlpha, dt * 5.5f);
                trackedEnemy.Widget.Modulate = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
