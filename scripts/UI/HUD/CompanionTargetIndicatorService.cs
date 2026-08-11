using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.UI.HUD
{
    /// <summary>
    /// Hiển thị mục tiêu mà companion đang thực sự chọn. Marker đổi màu khi LOS bị chặn,
    /// vừa là feedback cho người chơi vừa giúp debug AI mà không cần bật overlay dev.
    /// </summary>
    public partial class CompanionTargetIndicatorService : CanvasLayer
    {
        private sealed class MarkerEntry
        {
            public CombatCharacter Source;
            public CombatCharacter Target;
            public Label Marker;
            public bool ClearShot;
            public float Freshness;
        }

        public static CompanionTargetIndicatorService Instance { get; private set; }
        private readonly Dictionary<ulong, MarkerEntry> _markers = new();

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;
            Layer = 58;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static CompanionTargetIndicatorService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }
            if (tree?.Root == null)
            {
                return null;
            }
            var service = new CompanionTargetIndicatorService { Name = "CompanionTargetIndicatorService" };
            tree.Root.AddChild(service);
            return service;
        }

        public void SetTarget(CombatCharacter source, CombatCharacter target, bool clearShot)
        {
            if (source == null)
            {
                return;
            }

            ulong id = source.GetInstanceId();
            if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsAlive)
            {
                ClearTarget(source);
                return;
            }

            if (!_markers.TryGetValue(id, out MarkerEntry entry))
            {
                var marker = new Label
                {
                    Text = "▼",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    TopLevel = true,
                    ZIndex = 180
                };
                marker.AddThemeFontSizeOverride("font_size", 13);
                marker.AddThemeColorOverride("font_outline_color", Colors.Black);
                marker.AddThemeConstantOverride("outline_size", 3);
                AddChild(marker);

                entry = new MarkerEntry { Source = source, Marker = marker };
                _markers[id] = entry;
            }

            entry.Target = target;
            entry.ClearShot = clearShot;
            entry.Freshness = 0.35f;
            EnemyHealthBarService.Instance?.NotifyTargeted(target);
        }

        public void ClearTarget(CombatCharacter source)
        {
            if (source == null)
            {
                return;
            }

            ulong id = source.GetInstanceId();
            if (_markers.TryGetValue(id, out MarkerEntry entry))
            {
                entry.Marker?.QueueFree();
                _markers.Remove(id);
            }
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            var remove = new List<ulong>();
            foreach (var pair in _markers)
            {
                MarkerEntry entry = pair.Value;
                entry.Freshness -= dt;
                if (entry.Source == null || entry.Target == null
                    || !GodotObject.IsInstanceValid(entry.Source)
                    || !GodotObject.IsInstanceValid(entry.Target)
                    || !entry.Target.IsAlive
                    || entry.Freshness <= 0f)
                {
                    entry.Marker?.QueueFree();
                    remove.Add(pair.Key);
                    continue;
                }

                Color color = entry.ClearShot
                    ? new Color(0.42f, 0.92f, 1f)
                    : new Color(1f, 0.58f, 0.22f);
                entry.Marker.AddThemeColorOverride("font_color", color);
                Vector2 screenPos = entry.Target.GetGlobalTransformWithCanvas().Origin + new Vector2(0f, -44f);
                Vector2 size = entry.Marker.GetCombinedMinimumSize();
                entry.Marker.Position = screenPos - new Vector2(size.X * 0.5f, size.Y);
            }

            foreach (ulong id in remove)
            {
                _markers.Remove(id);
            }
        }
    }
}
