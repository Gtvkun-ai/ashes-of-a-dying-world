using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Skills;

namespace AshesofaDyingWorld.UI.HUD
{
    /// <summary>
    /// HUD cooldown cho 4 slot của nhân vật đang được điều khiển. Không còn khóa cứng vào Hikaru,
    /// nên switch sang Hyou sẽ đổi icon/cooldown theo loadout của Hyou ngay lập tức.
    /// </summary>
    public partial class SkillCooldownHudService : CanvasLayer
    {
        private sealed class SlotView
        {
            public Control Holder;
            public TextureRect Icon;
            public ColorRect CooldownOverlay;
            public Label CooldownLabel;
            public Label SlotLabel;
            public SkillData Skill;
            public float PreviousRemaining;
            public float ReadyPulse;
        }

        public static SkillCooldownHudService Instance { get; private set; }

        private readonly List<SlotView> _slots = new();
        private HBoxContainer _row;

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }

            Instance = this;
            Layer = 62;
            BuildUi();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static SkillCooldownHudService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }
            if (tree?.Root == null)
            {
                return null;
            }
            var service = new SkillCooldownHudService { Name = "SkillCooldownHudService" };
            tree.Root.AddChild(service);
            return service;
        }

        public override void _Process(double delta)
        {
            CombatCharacter actor = PlayerManager.Instance?.GetActiveCombatCharacter();
            if (actor == null || !actor.IsAlive)
            {
                _row.Visible = false;
                return;
            }

            var collection = SkillCollectionResolver.Resolve(actor.Stats);
            if (collection == null)
            {
                _row.Visible = false;
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            bool any = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                SlotView view = _slots[i];
                Key boundKey = SettingsManager.Instance?.GetSkillKey(i) ?? Key.None;
                view.SlotLabel.Text = boundKey == Key.None ? "?" : boundKey.ToString().ToUpperInvariant();

                SkillData skill = collection.GetEquippedSkill(i);
                if (view.Skill != skill)
                {
                    view.Skill = skill;
                    view.Icon.Texture = skill?.Icon;
                    view.PreviousRemaining = 0f;
                }

                bool visible = skill != null;
                view.Holder.Visible = visible;
                if (!visible)
                {
                    continue;
                }
                any = true;

                float remaining = actor.Abilities?.GetCooldownRemaining(skill) ?? 0f;
                float duration = Mathf.Max(0.01f, skill.Cooldown);
                float ratio = Mathf.Clamp(remaining / duration, 0f, 1f);
                float size = 38f;
                view.CooldownOverlay.Visible = ratio > 0.001f;
                view.CooldownOverlay.Position = new Vector2(0f, size * (1f - ratio));
                view.CooldownOverlay.Size = new Vector2(size, size * ratio);
                view.CooldownLabel.Text = remaining > 0.05f
                    ? (remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"))
                    : string.Empty;

                if (view.PreviousRemaining > 0.05f && remaining <= 0.05f)
                {
                    view.ReadyPulse = 0.22f;
                }
                view.PreviousRemaining = remaining;

                if (view.ReadyPulse > 0f)
                {
                    view.ReadyPulse -= dt;
                    float pulse = Mathf.Sin(Mathf.Clamp(view.ReadyPulse / 0.22f, 0f, 1f) * Mathf.Pi);
                    view.Holder.Scale = Vector2.One * (1f + pulse * 0.16f);
                }
                else
                {
                    view.Holder.Scale = Vector2.One;
                }
            }

            _row.Visible = any;
        }

        private void BuildUi()
        {
            _row = new HBoxContainer
            {
                Name = "SkillCooldownRow",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            // Đưa cụm hồi chiêu về vùng hotbar phía dưới thay vì lơ lửng cạnh HUD party.
            // Như vậy Hyou đứng đâu cũng không che mất thông tin cooldown nữa.
            _row.AnchorLeft = 0.5f;
            _row.AnchorTop = 1f;
            _row.AnchorRight = 0.5f;
            _row.AnchorBottom = 1f;
            _row.OffsetLeft = -86f;
            _row.OffsetTop = -118f;
            _row.OffsetRight = 86f;
            _row.OffsetBottom = -74f;
            _row.AddThemeConstantOverride("separation", 6);
            AddChild(_row);

            for (int i = 0; i < PlayerSkillCollection.SlotCount; i++)
            {
                var holder = new Control
                {
                    CustomMinimumSize = new Vector2(38f, 38f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    PivotOffset = new Vector2(19f, 19f)
                };
                _row.AddChild(holder);

                var background = new ColorRect
                {
                    Color = new Color(0.035f, 0.045f, 0.055f, 0.88f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Size = new Vector2(38f, 38f)
                };
                holder.AddChild(background);

                var icon = new TextureRect
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Position = new Vector2(3f, 3f),
                    Size = new Vector2(32f, 32f)
                };
                holder.AddChild(icon);

                var overlay = new ColorRect
                {
                    Color = new Color(0.02f, 0.025f, 0.035f, 0.72f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Size = new Vector2(38f, 0f)
                };
                holder.AddChild(overlay);

                var cooldown = new Label
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Size = new Vector2(38f, 38f)
                };
                cooldown.AddThemeFontSizeOverride("font_size", 11);
                cooldown.AddThemeColorOverride("font_color", Colors.White);
                cooldown.AddThemeColorOverride("font_outline_color", Colors.Black);
                cooldown.AddThemeConstantOverride("outline_size", 3);
                holder.AddChild(cooldown);

                var slotLabel = new Label
                {
                    Text = i switch { 0 => "Q", 1 => "E", 2 => "R", _ => "F" },
                    Position = new Vector2(2f, 20f),
                    Size = new Vector2(16f, 14f),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                slotLabel.AddThemeFontSizeOverride("font_size", 8);
                slotLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.92f));
                slotLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
                slotLabel.AddThemeConstantOverride("outline_size", 2);
                holder.AddChild(slotLabel);

                _slots.Add(new SlotView
                {
                    Holder = holder,
                    Icon = icon,
                    CooldownOverlay = overlay,
                    CooldownLabel = cooldown,
                    SlotLabel = slotLabel
                });
            }
        }
    }
}
