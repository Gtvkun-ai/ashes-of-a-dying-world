using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.Shared
{
    /// <summary>
    /// Skin nút dùng chung cho toàn bộ menu/panel.
    ///
    /// Chỉ cần đặt 9 PNG vào res://assets/graphics/ui/buttons với đúng tên:
    /// button_primary_normal.png / hover / pressed
    /// button_secondary_normal.png / hover / pressed
    /// button_danger_normal.png / hover / pressed
    ///
    /// Texture được vẽ bằng StyleBoxTexture (9-slice), nên một asset có thể co giãn
    /// cho nút 90 px, 165 px hay 300 px mà không phải căn/cắt lại từng ảnh.
    /// Nếu PNG chưa tồn tại, UI tự rơi về StyleBoxFlat để project vẫn chạy.
    /// </summary>
    public static class PixelButtonSkin
    {
        public enum Variant
        {
            Primary,
            Secondary,
            Danger
        }

        public const string AssetRoot = "res://assets/graphics/ui/buttons";

        public const float CompactHeight = 32f;
        public const float TabHeight = 34f;
        public const float RegularHeight = 38f;
        public const float LargeActionHeight = 44f;
        public const float FeatureTileWidth = 120f;
        public const float FeatureTileHeight = 80f;

        private const float DefaultMinHeight = RegularHeight;
        private const float HorizontalContentPadding = 12f;
        private const float VerticalContentPadding = 7f;
        private const int MaximumSourceHeight = 64;

        private static readonly Dictionary<string, Texture2D> TextureCache = new();

        public static void ApplyPrimary(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Primary, minHeight, minWidth);
        }

        public static void ApplySecondary(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Secondary, minHeight, minWidth);
        }

        public static void ApplyDanger(Button button, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            Apply(button, Variant.Danger, minHeight, minWidth);
        }

        /// <summary>
        /// Tab/filter dùng secondary khi nghỉ và primary khi đang chọn.
        /// Như vậy toàn UI chỉ cần đúng 9 texture, không sinh thêm một họ asset tab riêng.
        /// </summary>
        public static void ApplyTab(Button button, bool selected, float minHeight = TabHeight, float minWidth = 0f)
        {
            Apply(button, selected ? Variant.Primary : Variant.Secondary, minHeight, minWidth);
        }

        public static void Apply(Button button, Variant variant, float minHeight = DefaultMinHeight, float minWidth = 0f)
        {
            if (button == null)
            {
                return;
            }

            Vector2 currentMinimum = button.CustomMinimumSize;
            button.CustomMinimumSize = new Vector2(
                Mathf.Max(currentMinimum.X, minWidth),
                Mathf.Max(currentMinimum.Y, minHeight));

            // Pixel art phải giữ cạnh sắc. StyleBoxTexture lo phần 9-slice,
            // còn Button chỉ chịu trách nhiệm text/icon và input.
            button.Flat = false;
            button.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

            button.AddThemeStyleboxOverride("normal", CreateStateStyle(variant, "normal"));
            button.AddThemeStyleboxOverride("hover", CreateStateStyle(variant, "hover"));
            button.AddThemeStyleboxOverride("pressed", CreateStateStyle(variant, "pressed"));
            button.AddThemeStyleboxOverride("disabled", CreateDisabledStyle(variant));

            button.AddThemeColorOverride("font_color", GetTextColor(variant, false));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", new Color(0.72f, 0.68f, 0.62f, 0.52f));
            button.AddThemeColorOverride("icon_normal_color", Colors.White);
            button.AddThemeColorOverride("icon_hover_color", Colors.White);
            button.AddThemeColorOverride("icon_pressed_color", Colors.White);
            button.AddThemeColorOverride("icon_disabled_color", new Color(1f, 1f, 1f, 0.45f));
        }

        private static StyleBox CreateStateStyle(Variant variant, string state)
        {
            string path = $"{AssetRoot}/button_{VariantName(variant)}_{state}.png";
            Texture2D texture = LoadTexture(path);
            if (texture == null)
            {
                return CreateFallbackStyle(variant, state, false);
            }

            return CreateNineSliceStyle(texture, Colors.White);
        }

        private static StyleBox CreateDisabledStyle(Variant variant)
        {
            string path = $"{AssetRoot}/button_{VariantName(variant)}_normal.png";
            Texture2D texture = LoadTexture(path);
            if (texture == null)
            {
                return CreateFallbackStyle(variant, "normal", true);
            }

            // Không cần asset disabled thứ 10/11/12. Giảm sáng texture normal là đủ.
            return CreateNineSliceStyle(texture, new Color(0.56f, 0.53f, 0.49f, 0.62f));
        }

        private static StyleBoxTexture CreateNineSliceStyle(Texture2D texture, Color modulate)
        {
            Vector2I sourceSize = (Vector2I)texture.GetSize();
            float shortEdge = Mathf.Max(1f, Mathf.Min((float)sourceSize.X, (float)sourceSize.Y));

            // Margin tính từ chính kích thước source. Người dùng có thể thay PNG bằng
            // bản 512 px, 1024 px... mà không phải sửa code. Giới hạn 30% cạnh ngắn
            // để center vẫn còn đủ vùng stretch ngay cả với button khá nhỏ.
            float patch = Mathf.Clamp(Mathf.Round(shortEdge * 0.22f), 2f, shortEdge * 0.30f);

            var style = new StyleBoxTexture
            {
                Texture = texture,
                DrawCenter = true,
                ModulateColor = modulate,
                TextureMarginLeft = patch,
                TextureMarginTop = patch,
                TextureMarginRight = patch,
                TextureMarginBottom = patch,
                ContentMarginLeft = HorizontalContentPadding,
                ContentMarginRight = HorizontalContentPadding,
                ContentMarginTop = VerticalContentPadding,
                ContentMarginBottom = VerticalContentPadding
            };

            return style;
        }

        private static Texture2D LoadTexture(string path)
        {
            if (TextureCache.TryGetValue(path, out Texture2D cached))
            {
                return cached;
            }

            if (!ResourceLoader.Exists(path))
            {
                return null;
            }

            Texture2D importedTexture = GD.Load<Texture2D>(path);
            if (importedTexture == null)
            {
                return null;
            }

            Texture2D texture = NormalizeSourceTexture(importedTexture);
            TextureCache[path] = texture;
            return texture;
        }

        /// <summary>
        /// Asset AI/export thường có thể là 512-2048 px dù button trong game chỉ cao
        /// khoảng 32-80 px. Thu nhỏ source một lần lúc load để texture margin của 9-slice
        /// luôn hợp lý. Vì cache lại ImageTexture nên không có resize mỗi frame.
        /// </summary>
        private static Texture2D NormalizeSourceTexture(Texture2D source)
        {
            Vector2I sourceSize = (Vector2I)source.GetSize();
            if (sourceSize.Y <= MaximumSourceHeight || sourceSize.Y <= 0)
            {
                return source;
            }

            Image image = source.GetImage();
            if (image == null || image.IsEmpty())
            {
                return source;
            }

            float scale = MaximumSourceHeight / (float)sourceSize.Y;
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(sourceSize.X * scale));
            image.Resize(targetWidth, MaximumSourceHeight, Image.Interpolation.Nearest);
            return ImageTexture.CreateFromImage(image);
        }

        private static StyleBoxFlat CreateFallbackStyle(Variant variant, string state, bool disabled)
        {
            Color background;
            Color border;

            switch (variant)
            {
                case Variant.Primary:
                    background = new Color("#4a2f1e");
                    border = new Color("#c7934d");
                    break;
                case Variant.Danger:
                    background = new Color("#56231f");
                    border = new Color("#a7554a");
                    break;
                default:
                    background = new Color("#2a1d17");
                    border = new Color("#76543c");
                    break;
            }

            if (state == "hover")
            {
                background = background.Lightened(0.10f);
                border = border.Lightened(0.12f);
            }
            else if (state == "pressed")
            {
                background = background.Darkened(0.12f);
                border = border.Darkened(0.05f);
            }

            if (disabled)
            {
                background = new Color(background.R, background.G, background.B, 0.56f);
                border = new Color(border.R, border.G, border.B, 0.42f);
            }

            var style = new StyleBoxFlat
            {
                BgColor = background,
                BorderColor = border,
                ContentMarginLeft = HorizontalContentPadding,
                ContentMarginRight = HorizontalContentPadding,
                ContentMarginTop = VerticalContentPadding,
                ContentMarginBottom = VerticalContentPadding
            };
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(2);
            return style;
        }

        private static string VariantName(Variant variant)
        {
            return variant switch
            {
                Variant.Primary => "primary",
                Variant.Danger => "danger",
                _ => "secondary"
            };
        }

        private static Color GetTextColor(Variant variant, bool disabled)
        {
            if (disabled)
            {
                return new Color(0.72f, 0.68f, 0.62f, 0.52f);
            }

            return variant switch
            {
                Variant.Primary => new Color("#f4e6ca"),
                Variant.Danger => new Color("#f0c2b8"),
                _ => new Color("#dfd0bc")
            };
        }
    }
}
