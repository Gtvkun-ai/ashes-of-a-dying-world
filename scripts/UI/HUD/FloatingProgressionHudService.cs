using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.UI.HUD
{
    /// <summary>
    /// HUD progression nổi trên đầu nhân vật.
    ///
    /// Mục tiêu:
    /// - Bỏ thanh XP cố định trên party HUD vì với party 2 người nó vừa rối vừa không rõ đại diện cho ai.
    /// - Chỉ hiện khi nhân vật vừa nhận XP hoặc vừa lên cấp.
    /// - Neo trực tiếp lên nhân vật nhận XP, để người chơi nhìn là hiểu ngay progression gắn với ai.
    /// </summary>
    public partial class FloatingProgressionHudService : CanvasLayer
    {
        private sealed class EntryView
        {
            public PlayerStats Stats;
            public CombatCharacter Actor;
            public PanelContainer Root;
            public Label HeaderLabel;
            public ProgressBar ProgressBar;
            public Label DetailLabel;
            public float VisibleTimer;
            public int LastLevel;
            public int LastExperience;
            public int LastRequired;
            public bool Bound;
            public PlayerStats.StatsChangedEventHandler StatsChangedHandler;
            public PlayerStats.LevelChangedEventHandler LevelChangedHandler;
        }

        private static readonly Vector2 PanelSize = new(144f, 38f);
        private static readonly Vector2 WorldOffset = new(-72f, -62f);
        private const float ShowDurationSeconds = 1.45f;
        private const float FadeDurationSeconds = 0.22f;

        public static FloatingProgressionHudService Instance { get; private set; }

        private readonly Dictionary<PlayerStats, EntryView> _entries = new();
        private Control _root;
        private StyleBoxFlat _panelStyle;
        private StyleBoxFlat _barBackgroundStyle;
        private StyleBoxFlat _barFillStyle;

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }

            Instance = this;
            Layer = 61;
            BuildUiRoot();

            PlayerManager manager = PlayerManager.GetOrCreate(GetTree());
            if (manager != null)
            {
                manager.PartyUpdated += OnPartyUpdated;
            }

            SyncPartyMembers();
        }

        public override void _ExitTree()
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager != null)
            {
                manager.PartyUpdated -= OnPartyUpdated;
            }

            foreach (EntryView entry in _entries.Values)
            {
                UnbindEntry(entry);
            }
            _entries.Clear();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static FloatingProgressionHudService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }
            if (tree?.Root == null)
            {
                return null;
            }

            var service = new FloatingProgressionHudService { Name = "FloatingProgressionHudService" };
            tree.Root.AddChild(service);
            return service;
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            Transform2D canvasTransform = GetViewport().GetCanvasTransform();

            foreach (EntryView entry in _entries.Values)
            {
                if (entry.Root == null)
                {
                    continue;
                }

                entry.Actor = PlayerManager.Instance?.GetCombatCharacter(entry.Stats);
                if (entry.Actor == null)
                {
                    entry.Root.Visible = false;
                    continue;
                }

                if (entry.VisibleTimer <= 0f)
                {
                    entry.Root.Visible = false;
                    continue;
                }

                entry.VisibleTimer = Mathf.Max(0f, entry.VisibleTimer - dt);
                Vector2 screenPosition = canvasTransform * entry.Actor.GlobalPosition;
                entry.Root.Position = screenPosition + WorldOffset;
                entry.Root.Visible = true;

                float alpha = entry.VisibleTimer < FadeDurationSeconds
                    ? Mathf.Clamp(entry.VisibleTimer / FadeDurationSeconds, 0f, 1f)
                    : 1f;
                entry.Root.Modulate = new Color(1f, 1f, 1f, alpha);
            }
        }

        private void BuildUiRoot()
        {
            _root = new Control
            {
                Name = "FloatingProgressionRoot",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.04f, 0.03f, 0.92f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.58f, 0.38f, 0.16f, 0.95f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ContentMarginLeft = 5,
                ContentMarginTop = 4,
                ContentMarginRight = 5,
                ContentMarginBottom = 4,
            };

            _barBackgroundStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.03f, 0.026f, 0.02f, 0.95f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
            };

            _barFillStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.72f, 0.48f, 0.2f, 0.96f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
            };
        }

        private void OnPartyUpdated()
        {
            SyncPartyMembers();
        }

        private void SyncPartyMembers()
        {
            PlayerManager manager = PlayerManager.Instance;
            var validMembers = new HashSet<PlayerStats>(manager?.PartyMembers ?? new List<PlayerStats>());

            // Gỡ binding của thành viên không còn trong party.
            var toRemove = new List<PlayerStats>();
            foreach (var pair in _entries)
            {
                if (!validMembers.Contains(pair.Key))
                {
                    UnbindEntry(pair.Value);
                    pair.Value.Root?.QueueFree();
                    toRemove.Add(pair.Key);
                }
            }
            foreach (PlayerStats stats in toRemove)
            {
                _entries.Remove(stats);
            }

            if (manager == null)
            {
                return;
            }

            foreach (PlayerStats stats in manager.PartyMembers)
            {
                if (stats == null || _entries.ContainsKey(stats))
                {
                    continue;
                }

                EntryView entry = CreateEntry(stats);
                _entries.Add(stats, entry);
                BindEntry(entry);
            }
        }

        private EntryView CreateEntry(PlayerStats stats)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = PanelSize,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.AddThemeStyleboxOverride("panel", _panelStyle);
            _root.AddChild(panel);

            var column = new VBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            column.AddThemeConstantOverride("separation", 2);
            panel.AddChild(column);

            var header = new Label
            {
                Text = "LV 01",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            header.AddThemeFontSizeOverride("font_size", 10);
            header.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.65f));
            header.AddThemeColorOverride("font_outline_color", Colors.Black);
            header.AddThemeConstantOverride("outline_size", 2);
            column.AddChild(header);

            var bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0f, 7f),
                ShowPercentage = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                MaxValue = 1,
                Value = 0
            };
            bar.AddThemeStyleboxOverride("background", _barBackgroundStyle);
            bar.AddThemeStyleboxOverride("fill", _barFillStyle);
            column.AddChild(bar);

            var detail = new Label
            {
                Text = "100 XP nữa",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            detail.AddThemeFontSizeOverride("font_size", 9);
            detail.AddThemeColorOverride("font_color", new Color(0.82f, 0.74f, 0.63f));
            detail.AddThemeColorOverride("font_outline_color", Colors.Black);
            detail.AddThemeConstantOverride("outline_size", 2);
            column.AddChild(detail);

            return new EntryView
            {
                Stats = stats,
                Actor = PlayerManager.Instance?.GetCombatCharacter(stats),
                Root = panel,
                HeaderLabel = header,
                ProgressBar = bar,
                DetailLabel = detail,
                LastLevel = stats.CurrentLevel,
                LastExperience = stats.CurrentExperience,
                LastRequired = Mathf.Max(1, stats.ExperienceToNextLevel),
            };
        }

        private void BindEntry(EntryView entry)
        {
            if (entry?.Stats == null || entry.Bound)
            {
                return;
            }

            // Khởi tạo snapshot hiện tại nhưng chưa hiện UI; đợi lúc thật sự có tiến độ XP.
            entry.LastLevel = entry.Stats.CurrentLevel;
            entry.LastExperience = entry.Stats.CurrentExperience;
            entry.LastRequired = Mathf.Max(1, entry.Stats.ExperienceToNextLevel);

            entry.StatsChangedHandler = () => OnEntryStatsChanged(entry);
            entry.LevelChangedHandler = newLevel => OnEntryLevelChanged(entry, newLevel);
            entry.Stats.StatsChanged += entry.StatsChangedHandler;
            entry.Stats.LevelChanged += entry.LevelChangedHandler;
            entry.Bound = true;
        }

        private void UnbindEntry(EntryView entry)
        {
            if (entry?.Stats == null || !entry.Bound)
            {
                return;
            }

            if (entry.StatsChangedHandler != null)
            {
                entry.Stats.StatsChanged -= entry.StatsChangedHandler;
            }
            if (entry.LevelChangedHandler != null)
            {
                entry.Stats.LevelChanged -= entry.LevelChangedHandler;
            }
            entry.Bound = false;
        }

        private void OnEntryStatsChanged(EntryView entry)
        {
            PlayerStats stats = entry?.Stats;
            if (stats == null)
            {
                return;
            }

            int required = Mathf.Max(1, stats.ExperienceToNextLevel);
            bool progressionChanged = stats.CurrentExperience != entry.LastExperience
                || stats.CurrentLevel != entry.LastLevel
                || required != entry.LastRequired;
            if (!progressionChanged)
            {
                return;
            }

            // Nếu level vừa đổi, callback LevelChanged sẽ lo popup riêng.
            if (stats.CurrentLevel == entry.LastLevel)
            {
                ShowProgress(stats, levelUp: false);
            }
            else
            {
                entry.LastLevel = stats.CurrentLevel;
                entry.LastExperience = stats.CurrentExperience;
                entry.LastRequired = required;
            }
        }

        private void OnEntryLevelChanged(EntryView entry, int newLevel)
        {
            PlayerStats stats = entry?.Stats;
            if (stats == null)
            {
                return;
            }

            ShowProgress(stats, levelUp: true);
        }

        private void ShowProgress(PlayerStats stats, bool levelUp)
        {
            if (stats == null || !_entries.TryGetValue(stats, out EntryView entry))
            {
                return;
            }

            int required = stats.IsAtMaxLevel ? 1 : Mathf.Max(1, stats.ExperienceToNextLevel);
            int current = stats.IsAtMaxLevel ? 1 : Mathf.Clamp(stats.CurrentExperience, 0, required);

            entry.ProgressBar.MaxValue = required;
            entry.ProgressBar.Value = current;
            entry.ProgressBar.TooltipText = stats.IsAtMaxLevel
                ? "Đã đạt cấp tối đa"
                : $"{current:N0} / {required:N0} XP";

            if (levelUp)
            {
                entry.HeaderLabel.Text = "LÊN CẤP!";
                entry.DetailLabel.Text = $"LV {stats.CurrentLevel:00}";
            }
            else
            {
                entry.HeaderLabel.Text = $"LV {stats.CurrentLevel:00}";
                entry.DetailLabel.Text = stats.IsAtMaxLevel
                    ? "MAX"
                    : $"{stats.ExperienceRemaining:N0} XP nữa";
            }

            entry.Root.Modulate = Colors.White;
            entry.VisibleTimer = ShowDurationSeconds;
            entry.LastLevel = stats.CurrentLevel;
            entry.LastExperience = stats.CurrentExperience;
            entry.LastRequired = required;
        }
    }
}
