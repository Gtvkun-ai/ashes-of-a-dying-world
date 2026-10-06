using System;
using Godot;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.UI.Theme;

namespace AshesofaDyingWorld.Validation
{
    /// <summary>
    /// Builds the code-native component and world-HUD portion of the visual showcase.
    /// Party and Dialogic samples stay in GDScript because their validation fixtures
    /// already use those runtime APIs directly.
    /// </summary>
    public partial class UiVisualShowcaseComponents : Node
    {
        private Node2D _enemy;
        private float _enemyHp = 62f;

        public override void _Ready()
        {
            Control root = GetParentOrNull<Control>();
            if (root == null)
            {
                GD.PrintErr("[UiVisualShowcase] Control root is missing.");
                return;
            }

            UiThemeFactory.Apply(root);
            BuildComponentPanel(root);
            BuildWorldHudSample(root);
        }

        private static void BuildComponentPanel(Control root)
        {
            var panel = new PanelContainer
            {
                Name = "ComponentPanel",
                Position = new Vector2(24f, 24f),
                Size = new Vector2(430f, 286f),
                CustomMinimumSize = new Vector2(430f, 286f)
            };
            panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
            root.AddChild(panel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", UiTokens.Space4);
            margin.AddThemeConstantOverride("margin_top", UiTokens.Space4);
            margin.AddThemeConstantOverride("margin_right", UiTokens.Space4);
            margin.AddThemeConstantOverride("margin_bottom", UiTokens.Space4);
            panel.AddChild(margin);

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", UiTokens.Space3);
            margin.AddChild(column);

            Label eyebrow = CreateLabel("RUNTIME COMPONENTS", UiTextRole.Micro, UiTokens.Accent);
            column.AddChild(eyebrow);
            Label title = CreateLabel("Trạng thái UI", UiTextRole.SectionTitle, UiTokens.TextPrimary);
            column.AddChild(title);

            var buttonFlow = new HFlowContainer();
            buttonFlow.AddThemeConstantOverride("h_separation", UiTokens.Space2);
            buttonFlow.AddThemeConstantOverride("v_separation", UiTokens.Space2);
            column.AddChild(buttonFlow);

            Button primary = CreateButton("Chọn", UiGlyph.Target);
            PixelButtonSkin.ApplyPrimary(primary, PixelButtonSkin.RegularHeight, 104f);
            buttonFlow.AddChild(primary);

            Button secondary = CreateButton("Hành trang", UiGlyph.Inventory);
            PixelButtonSkin.ApplySecondary(secondary, PixelButtonSkin.RegularHeight, 130f);
            buttonFlow.AddChild(secondary);

            Button danger = CreateButton("Rời đi", UiGlyph.Exit);
            PixelButtonSkin.ApplyDanger(danger, PixelButtonSkin.RegularHeight, 112f);
            buttonFlow.AddChild(danger);

            Button disabled = CreateButton("Chưa mở", UiGlyph.CategoryMap);
            PixelButtonSkin.ApplySecondary(disabled, PixelButtonSkin.RegularHeight, 118f);
            disabled.Disabled = true;
            buttonFlow.AddChild(disabled);

            Label glyphLabel = CreateLabel("GLYPH 24 PX · INTEGER CELLS", UiTextRole.Micro, UiTokens.TextSecondary);
            column.AddChild(glyphLabel);

            var glyphRow = new HBoxContainer();
            glyphRow.AddThemeConstantOverride("separation", UiTokens.Space3);
            column.AddChild(glyphRow);
            foreach (UiGlyph glyph in new[]
            {
                UiGlyph.Menu,
                UiGlyph.Character,
                UiGlyph.Inventory,
                UiGlyph.Skills,
                UiGlyph.Quests,
                UiGlyph.Party,
                UiGlyph.Settings
            })
            {
                var holder = new Button
                {
                    CustomMinimumSize = new Vector2(36f, 36f),
                    TooltipText = glyph.ToString(),
                    FocusMode = Control.FocusModeEnum.All,
                    Icon = UiGlyphResolver.Resolve(glyph),
                    IconAlignment = HorizontalAlignment.Center,
                    ExpandIcon = false
                };
                holder.AddThemeStyleboxOverride("normal", CreateGlyphStyle());
                holder.AddThemeStyleboxOverride("hover", CreateGlyphFocusStyle());
                holder.AddThemeStyleboxOverride("focus", CreateGlyphFocusStyle());
                holder.AddThemeStyleboxOverride("pressed", CreateGlyphFocusStyle());
                holder.AddThemeColorOverride("icon_normal_color", UiTokens.TextPrimary);
                holder.AddThemeColorOverride("icon_hover_color", Colors.White);
                holder.AddThemeColorOverride("icon_focus_color", Colors.White);
                holder.AddThemeColorOverride("icon_pressed_color", Colors.White);
                holder.FocusEntered += () => glyphLabel.Text = $"{glyph.ToString().ToUpperInvariant()} · 24 PX";
                holder.MouseEntered += () => glyphLabel.Text = $"{glyph.ToString().ToUpperInvariant()} · 24 PX";
                glyphRow.AddChild(holder);
            }

            primary.GrabFocus();
        }

        private void BuildWorldHudSample(Control root)
        {
            Vector2 viewportSize = root.GetViewportRect().Size;
            var camera = new Camera2D
            {
                Name = "ShowcaseCamera",
                Position = viewportSize * 0.5f
            };
            root.AddChild(camera);
            camera.MakeCurrent();

            _enemy = new Node2D
            {
                Name = "ShowcaseEnemy",
                Position = new Vector2(viewportSize.X * 0.59f, viewportSize.Y * 0.46f)
            };
            root.AddChild(_enemy);

            var silhouette = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(-28f, 20f),
                    new Vector2(-18f, -20f),
                    new Vector2(0f, -34f),
                    new Vector2(20f, -18f),
                    new Vector2(30f, 22f),
                    new Vector2(0f, 34f)
                },
                Color = UiTokens.Danger.Darkened(0.22f)
            };
            _enemy.AddChild(silhouette);

            var core = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(-10f, 9f),
                    new Vector2(0f, -14f),
                    new Vector2(11f, 9f),
                    new Vector2(0f, 16f)
                },
                Color = UiTokens.TextPrimary
            };
            _enemy.AddChild(core);

            EnemyHealthBarService health = EnemyHealthBarService.GetOrCreate(GetTree());
            health?.RegisterEnemy(_enemy, () => _enemyHp, () => 100f, () => 9);
            health?.NotifyDamaged(_enemy);

            DamageNumberService damage = DamageNumberService.GetOrCreate(GetTree());
            damage?.ShowDamage(_enemy, 138f, shattered: false, ice: false, blocked: false);

            var marker = new TextureRect
            {
                Name = "TargetMarkerSample",
                Texture = UiGlyphResolver.Resolve(UiGlyph.Target),
                Position = _enemy.Position + new Vector2(-12f, -194f),
                Size = new Vector2(UiTokens.IconSize, UiTokens.IconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = UiTokens.Ice
            };
            root.AddChild(marker);

            Label caption = CreateLabel("TARGET · HEALTH · FEEDBACK", UiTextRole.Micro, UiTokens.TextPrimary);
            caption.Name = "WorldHudCaption";
            caption.Position = _enemy.Position + new Vector2(-104f, 58f);
            root.AddChild(caption);
        }

        private static Button CreateButton(string text, UiGlyph glyph)
        {
            return new Button
            {
                Text = text,
                Icon = UiGlyphResolver.Resolve(glyph),
                IconAlignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
        }

        private static Label CreateLabel(string text, UiTextRole role, Color color)
        {
            var label = new Label { Text = text };
            UiThemeFactory.ApplyText(label, role);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        private static StyleBoxFlat CreatePanelStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(UiTokens.Background, 0.96f),
                BorderColor = UiTokens.BorderStrong
            };
            style.SetBorderWidthAll(UiTokens.BorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);
            return style;
        }

        private static StyleBoxFlat CreateGlyphStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = UiTokens.Canvas,
                BorderColor = UiTokens.Border
            };
            style.SetBorderWidthAll(UiTokens.BorderWidth);
            style.SetCornerRadiusAll(UiTokens.CornerRadius);
            return style;
        }

        private static StyleBoxFlat CreateGlyphFocusStyle()
        {
            StyleBoxFlat style = CreateGlyphStyle();
            style.BgColor = UiTokens.SurfaceRaised;
            style.BorderColor = UiTokens.Accent;
            style.SetBorderWidthAll(UiTokens.SelectionBorderWidth);
            return style;
        }
    }
}
