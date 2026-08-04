using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.UI.HUD.Skills;

namespace AshesofaDyingWorld.UI.Skills
{
    /// <summary>
    /// Trạng thái hình ảnh của một node trên cây kỹ năng.
    /// </summary>
    public enum SkillTreeNodeVisualState
    {
        Locked,
        Unlockable,
        Unlocked
    }

    /// <summary>
    /// Nút kỹ năng độc lập. Lớp này chỉ lo trình bày và click;
    /// luật mở khóa nằm trong SkillTreeProgression.
    /// </summary>
    public partial class SkillTreeNodeView : Button
    {
        public static readonly Vector2 NodeSize = new(104f, 94f);
        public SkillTreeNodeData Data { get; private set; }

        public void Configure(
            SkillTreeNodeData data,
            SkillTreeNodeVisualState state,
            Color accent,
            bool selected)
        {
            Data = data;
            CustomMinimumSize = NodeSize;
            Size = NodeSize;
            FocusMode = FocusModeEnum.None;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            Text = data?.Skill?.SkillName ?? "Kỹ năng";
            Icon = SkillIconResolver.Resolve(data?.Skill);
            ExpandIcon = true;
            IconAlignment = HorizontalAlignment.Center;
            VerticalIconAlignment = VerticalAlignment.Top;
            AddThemeConstantOverride("icon_max_width", 46);
            AddThemeFontSizeOverride("font_size", 11);
            AddThemeColorOverride("font_color", ResolveTextColor(state));
            AddThemeColorOverride("font_hover_color", InventoryPanelChrome.MainTextColor);
            AddThemeColorOverride("font_pressed_color", Colors.White);
            AddThemeStyleboxOverride("normal", CreateNodeStyle(state, accent, selected, false));
            AddThemeStyleboxOverride("hover", CreateNodeStyle(state, accent, true, true));
            AddThemeStyleboxOverride("pressed", CreateNodeStyle(state, accent, true, true));
            Modulate = state == SkillTreeNodeVisualState.Locked
                ? new Color(0.72f, 0.72f, 0.72f, 0.78f)
                : Colors.White;

            string stateText = state switch
            {
                SkillTreeNodeVisualState.Unlocked => "Đã mở",
                SkillTreeNodeVisualState.Unlockable => "Có thể mở",
                _ => "Đang khóa"
            };
            TooltipText = $"{Text}\n{stateText}";
        }

        private static Color ResolveTextColor(SkillTreeNodeVisualState state)
        {
            return state == SkillTreeNodeVisualState.Locked
                ? InventoryPanelChrome.MutedTextColor.Darkened(0.18f)
                : InventoryPanelChrome.MainTextColor;
        }

        private static StyleBoxFlat CreateNodeStyle(
            SkillTreeNodeVisualState state,
            Color accent,
            bool selected,
            bool hovered)
        {
            Color background = state switch
            {
                SkillTreeNodeVisualState.Unlocked => new Color(accent.R, accent.G, accent.B, hovered ? 0.28f : 0.19f),
                SkillTreeNodeVisualState.Unlockable => InventoryPanelChrome.RaisedSurfaceColor.Lightened(hovered ? 0.08f : 0.02f),
                _ => InventoryPanelChrome.DeepSurfaceColor.Darkened(0.03f)
            };

            Color border = state switch
            {
                SkillTreeNodeVisualState.Unlocked => accent,
                SkillTreeNodeVisualState.Unlockable => InventoryPanelChrome.AccentColor,
                _ => InventoryPanelChrome.BorderColor.Darkened(0.22f)
            };

            var style = new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = selected ? accent.Lightened(0.15f) : border,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 8,
                ContentMarginBottom = 6
            };
            style.SetBorderWidthAll(selected ? 3 : 2);
            style.SetCornerRadiusAll(10);

            if (state == SkillTreeNodeVisualState.Unlocked || selected)
            {
                style.ShadowColor = new Color(accent.R, accent.G, accent.B, selected ? 0.38f : 0.22f);
                style.ShadowSize = selected ? 8 : 5;
            }

            return style;
        }

    }
}
