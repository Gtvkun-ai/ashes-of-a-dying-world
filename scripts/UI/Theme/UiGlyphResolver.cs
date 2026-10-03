using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.UI.Theme
{
    public enum UiGlyph
    {
        Menu = 0,
        Character = 1,
        Inventory = 2,
        Skills = 3,
        Quests = 4,
        Party = 5,
        Settings = 6,
        Target = 7,

        CategoryWeapon = 8,
        CategoryArmor = 9,
        CategoryConsumable = 10,
        CategoryMaterial = 11,
        CategoryQuest = 12,
        CategoryMap = 13,
        CategoryCurrency = 14,
        CategoryKeyItem = 15,

        Strength = 16,
        Dexterity = 17,
        Intelligence = 18,
        Vitality = 19,
        Spirit = 20,
        Defense = 21,
        Exit = 22,
        Fallback = 23,

        STR = Strength,
        DEX = Dexterity,
        INT = Intelligence,
        VIT = Vitality,
        SPI = Spirit,
        DEF = Defense,
        TargetMarker = Target,
        Unknown = Fallback
    }

    /// <summary>
    /// Provides stable 24px atlas regions for shared UI glyphs.
    /// </summary>
    public static class UiGlyphResolver
    {
        private const string AtlasPath = "res://assets/graphics/ui/icons/ui_glyph_atlas.svg";
        private const int CellSize = UiTokens.IconSize;

        private static readonly Dictionary<UiGlyph, Rect2> Regions = new()
        {
            [UiGlyph.Menu] = Cell(0, 0),
            [UiGlyph.Character] = Cell(1, 0),
            [UiGlyph.Inventory] = Cell(2, 0),
            [UiGlyph.Skills] = Cell(3, 0),
            [UiGlyph.Quests] = Cell(4, 0),
            [UiGlyph.Party] = Cell(5, 0),
            [UiGlyph.Settings] = Cell(6, 0),
            [UiGlyph.Target] = Cell(7, 0),

            [UiGlyph.CategoryWeapon] = Cell(0, 1),
            [UiGlyph.CategoryArmor] = Cell(1, 1),
            [UiGlyph.CategoryConsumable] = Cell(2, 1),
            [UiGlyph.CategoryMaterial] = Cell(3, 1),
            [UiGlyph.CategoryQuest] = Cell(4, 1),
            [UiGlyph.CategoryMap] = Cell(5, 1),
            [UiGlyph.CategoryCurrency] = Cell(6, 1),
            [UiGlyph.CategoryKeyItem] = Cell(7, 1),

            [UiGlyph.Strength] = Cell(0, 2),
            [UiGlyph.Dexterity] = Cell(1, 2),
            [UiGlyph.Intelligence] = Cell(2, 2),
            [UiGlyph.Vitality] = Cell(3, 2),
            [UiGlyph.Spirit] = Cell(4, 2),
            [UiGlyph.Defense] = Cell(5, 2),
            [UiGlyph.Exit] = Cell(6, 2),
            [UiGlyph.Fallback] = Cell(7, 2)
        };

        private static Texture2D _atlas;
        private static ImageTexture _fallbackTexture;

        public static AtlasTexture Resolve(UiGlyph glyph)
        {
            _atlas ??= ResourceLoader.Exists(AtlasPath) ? GD.Load<Texture2D>(AtlasPath) : null;
            if (_atlas == null)
            {
                return CreateFallbackTexture();
            }

            Rect2 region = Regions.TryGetValue(glyph, out Rect2 mappedRegion)
                ? mappedRegion
                : Regions[UiGlyph.Fallback];

            return new AtlasTexture
            {
                Atlas = _atlas,
                Region = region
            };
        }

        private static AtlasTexture CreateFallbackTexture()
        {
            _fallbackTexture ??= BuildFallbackTexture();

            return new AtlasTexture
            {
                Atlas = _fallbackTexture,
                Region = new Rect2(0, 0, CellSize, CellSize)
            };
        }

        private static ImageTexture BuildFallbackTexture()
        {
            Image image = Image.CreateEmpty(CellSize, CellSize, false, Image.Format.Rgba8);
            image.Fill(new Color(0, 0, 0, 0));
            Color glyphColor = UiTokens.TextPrimary;

            FillFallbackStroke(image, glyphColor, 8, 4, 8, 2);
            FillFallbackStroke(image, glyphColor, 6, 6, 3, 2);
            FillFallbackStroke(image, glyphColor, 14, 6, 3, 2);
            FillFallbackStroke(image, glyphColor, 13, 8, 3, 2);
            FillFallbackStroke(image, glyphColor, 11, 10, 3, 2);
            FillFallbackStroke(image, glyphColor, 9, 12, 3, 3);
            FillFallbackStroke(image, glyphColor, 9, 18, 3, 2);

            return ImageTexture.CreateFromImage(image);
        }

        private static void FillFallbackStroke(Image image, Color color, int x, int y, int width, int height)
        {
            for (int pixelX = x; pixelX < x + width; pixelX++)
            {
                for (int pixelY = y; pixelY < y + height; pixelY++)
                {
                    image.SetPixel(pixelX, pixelY, color);
                }
            }
        }

        private static Rect2 Cell(int column, int row)
        {
            return new Rect2(column * CellSize, row * CellSize, CellSize, CellSize);
        }
    }
}
