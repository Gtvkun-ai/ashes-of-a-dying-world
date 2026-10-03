using Godot;

namespace AshesofaDyingWorld.UI.Theme
{
    public enum UiTextRole
    {
        ScreenTitle,
        SectionTitle,
        Body,
        Label,
        Micro,
        CombatNumber
    }

    /// <summary>
    /// Shared visual constants for the Living Ash UI. Components may add a
    /// semantic accent, but geometry and neutral colors originate here.
    /// </summary>
    public static class UiTokens
    {
        public static readonly Color Canvas = new("#120F0C");
        public static readonly Color Background = new("#1D1813");
        public static readonly Color Surface = new("#29221B");
        public static readonly Color SurfaceRaised = new("#352C22");
        public static readonly Color Border = new("#5C4B38");
        public static readonly Color BorderStrong = new("#8A6B42");
        public static readonly Color Accent = new("#C39A57");
        public static readonly Color TextPrimary = new("#F0E5D2");
        public static readonly Color TextSecondary = new("#BFAF98");
        public static readonly Color TextDisabled = new("#756B5D");
        public static readonly Color Danger = new("#B9574F");
        public static readonly Color Life = new("#7FA56D");
        public static readonly Color Memory = new("#9A72C7");
        public static readonly Color Ice = new("#62B8D9");

        public const int Grid = 4;
        public const int Space1 = Grid;
        public const int Space2 = Grid * 2;
        public const int Space3 = Grid * 3;
        public const int Space4 = Grid * 4;
        public const int Space6 = Grid * 6;
        public const int Space8 = Grid * 8;

        public const int BorderWidth = 1;
        public const int SelectionBorderWidth = 2;
        public const int CornerRadius = 3;

        public const int IconSizeSmall = 16;
        public const int IconSizeMedium = 20;
        public const int IconSize = 24;
        public const int IconSizeLarge = 32;
        public const int StatusIconSize = 24;
        public const int StatusFrameSize = 28;

        public const int ButtonHeightCompact = 32;
        public const int ButtonHeightRegular = 40;
        public const int ButtonHeightLarge = 48;

        public const int ScreenTitleFontSize = 24;
        public const int SectionTitleFontSize = 18;
        public const int BodyFontSize = 14;
        public const int LabelFontSize = 12;
        public const int MicroFontSize = 11;
        public const int CombatNumberFontSize = 20;
    }
}
