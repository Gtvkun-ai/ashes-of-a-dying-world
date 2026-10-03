using System;
using Godot;
using AshesofaDyingWorld.UI.Theme;

namespace AshesofaDyingWorld.Tools.Validation
{
    public partial class UiGlyphSmokeTest : Node
    {
        public override void _Ready()
        {
            try
            {
                RunSmokeChecks();
                GD.Print("UI glyph smoke test passed");
                GetTree().Quit(0);
            }
            catch (Exception error)
            {
                GD.PrintErr($"UI glyph smoke test failed: {error.Message}");
                GetTree().Quit(1);
            }
        }

        private static void RunSmokeChecks()
        {
            AtlasTexture menu = UiGlyphResolver.Resolve(UiGlyph.Menu);
            Require(menu.Atlas != null, "Menu glyph atlas is null");
            Require(menu.Region == Cell(0, 0), "Menu glyph region is not 24x24 at 0,0");

            AtlasTexture unknown = UiGlyphResolver.Resolve((UiGlyph)int.MaxValue);
            Require(unknown.Atlas != null, "Unknown glyph atlas is null");
            Require(unknown.Region == Cell(7, 2), "Unknown glyph did not resolve to the fallback atlas cell");

            UiGlyphResolver.InjectAtlasForValidation(null);
            try
            {
                AtlasTexture missingAtlas = UiGlyphResolver.Resolve(UiGlyph.Menu);
                Require(missingAtlas.Atlas != null, "Missing-atlas fallback is null");
                Require(missingAtlas.Atlas is ImageTexture, "Missing-atlas fallback is not an ImageTexture");
                Require(missingAtlas.Atlas.GetWidth() == UiTokens.IconSize, "Missing-atlas fallback width is not 24px");
                Require(missingAtlas.Atlas.GetHeight() == UiTokens.IconSize, "Missing-atlas fallback height is not 24px");
                Require(missingAtlas.Region == Cell(0, 0), "Missing-atlas fallback region is not 24x24 at 0,0");
            }
            finally
            {
                UiGlyphResolver.ClearAtlasForValidation();
            }
        }

        private static Rect2 Cell(int column, int row)
        {
            return new Rect2(
                column * UiTokens.IconSize,
                row * UiTokens.IconSize,
                UiTokens.IconSize,
                UiTokens.IconSize);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
