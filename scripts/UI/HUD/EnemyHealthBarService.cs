using Godot;
using System;
using System.Collections.Generic;

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
        }

        public static EnemyHealthBarService Instance { get; private set; }

        private readonly List<TrackedEnemy> _tracked = new();

        [Export] public Vector2 ScreenOffset = new(0, -30);
        [Export] public float WidgetScale = 1.0f;
        [Export] public Vector2 HpBarSize = new(40, 10);
        [Export] public float RowSpacing = 4f;
        [Export] public float LevelVerticalOffset = -1f;

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
            EnsureDefaultTextures();
        }

        private void EnsureDefaultTextures()
        {
            if (HpTextureProgress == null)
            {
                HpTextureProgress = GD.Load<Texture2D>(DefaultEnemyHpTexturePath);
            }

            if (HpTextureProgress == null)
            {
                GD.PrintErr($"[EnemyHealthBarService] Cannot load default HP texture: {DefaultEnemyHpTexturePath}");
            }
        }

        public void RegisterEnemy(Node2D enemy, Func<float> getCurrentHp, Func<float> getMaxHp, Func<int> getLevel)
        {
            if (enemy == null || getCurrentHp == null || getMaxHp == null || getLevel == null)
            {
                GD.PrintErr("Tham so khong hop le khi dang ky ke dich.");
                return;
            }

            foreach (var trackedEnemy in _tracked)
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
                ZIndex = 100
            };

            var levelLabel = new Label();
            levelLabel.AddThemeFontSizeOverride("font_size", 16);
            levelLabel.AddThemeColorOverride("font_color", Colors.White);
            levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
            widget.AddChild(levelLabel);

            var hpBar = new TextureProgressBar();
            hpBar.CustomMinimumSize = HpBarSize;
            hpBar.MaxValue = 100;
            hpBar.Value = 100;
            if (HpTextureUnder != null) hpBar.TextureUnder = HpTextureUnder;
            if (HpTextureProgress != null) hpBar.TextureProgress = HpTextureProgress;
            if (HpTextureOver != null) hpBar.TextureOver = HpTextureOver;

            widget.AddChild(hpBar);
            AddChild(widget);

            _tracked.Add(new TrackedEnemy
            {
                EnemyNode = enemy,
                GetCurrentHp = getCurrentHp,
                GetMaxHp = getMaxHp,
                GetLevel = getLevel,
                Widget = widget,
                Bar = hpBar,
                LevelLabel = levelLabel
            });
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
            var viewport = GetViewport();
            if (viewport == null)
            {
                return;
            }

            var camera = viewport.GetCamera2D();
            if (camera == null)
            {
                return;
            }

            // Lấy kích thước viewport để tính toán vị trí hiển thị
            Vector2 viewportSize = viewport.GetVisibleRect().Size;

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var trackedEnemy = _tracked[i];

                if (!GodotObject.IsInstanceValid(trackedEnemy.EnemyNode))
                {
                    trackedEnemy.Widget.QueueFree();
                    _tracked.RemoveAt(i);
                    continue;
                }

                float maxHp = Math.Max(1f, trackedEnemy.GetMaxHp());
                float curHp = Math.Clamp(trackedEnemy.GetCurrentHp(), 0f, maxHp);

                trackedEnemy.Bar.MaxValue = maxHp;
                trackedEnemy.Bar.Value = curHp;
                trackedEnemy.LevelLabel.Text = trackedEnemy.GetLevel().ToString();
                trackedEnemy.Widget.Scale = new Vector2(WidgetScale, WidgetScale);

                Vector2 levelSize = trackedEnemy.LevelLabel.GetCombinedMinimumSize();
                Vector2 barSize = trackedEnemy.Bar.GetCombinedMinimumSize(); // thường là HpBarSize nhưng vẫn lấy lại để đảm bảo đúng nếu texture có kích thước khác
                float baseHeight = Mathf.Max(levelSize.Y, barSize.Y); // chiều cao của hàng chứa cả level và hp bar, dùng để căn chỉnh theo chiều dọc
                float rawLevelY = (baseHeight - levelSize.Y) * 0.5f + LevelVerticalOffset; // căn giữa theo chiều dọc trong cùng một hàng với hp bar, cộng offset để có thể điều chỉnh vị trí level label cao hơn hoặc thấp hơn nếu cần, tránh lệch nhau khi level label có kích thước khác nhau
                float rawBarY = (baseHeight - barSize.Y) * 0.5f; // căn giữa theo chiều dọc trong cùng một hàng với level label, không cộng offset để tránh lệch nhau khi level label có kích thước khác nhau
                float minY = Mathf.Min(rawLevelY, rawBarY);

                Vector2 levelPos = new Vector2(0f, rawLevelY - minY);
                Vector2 barPos = new Vector2(levelSize.X + RowSpacing, rawBarY - minY);
                float rowWidth = levelSize.X + RowSpacing + barSize.X;
                float rowHeight = Mathf.Max(levelPos.Y + levelSize.Y, barPos.Y + barSize.Y);

                trackedEnemy.LevelLabel.Position = levelPos;
                trackedEnemy.Bar.Position = barPos;
                trackedEnemy.Widget.CustomMinimumSize = new Vector2(rowWidth, rowHeight);

                Vector2 worldPos = trackedEnemy.EnemyNode.GlobalPosition;
                // Tính vị trí tương đối so với camera và chuyển sang 
                Vector2 rel = (worldPos - camera.GlobalPosition) * camera.Zoom;
                Vector2 screenCenter = viewportSize / 2f;
                // Tính vị trí trên màn hình để đặt widget, cộng thêm offset tùy chỉnh
                Vector2 screenPos = trackedEnemy.EnemyNode.GetGlobalTransformWithCanvas().Origin + ScreenOffset;

                Vector2 widgetSize = trackedEnemy.Widget.GetCombinedMinimumSize() * WidgetScale;
                trackedEnemy.Widget.Position = screenPos - new Vector2(widgetSize.X / 2f, widgetSize.Y);
            }
        }
    }
}
