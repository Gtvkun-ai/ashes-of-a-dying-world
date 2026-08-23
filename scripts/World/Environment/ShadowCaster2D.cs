using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Adapter rất mỏng giữa Sprite2D/AnimatedSprite2D và shared Shadow Core material.
    ///
    /// V2.1 bỏ hoàn toàn quad padding + inverse texture mapping của V2. Shadow proxy giờ có
    /// đúng texture/transform của source; vertex shader chỉ biến cả quad thành một mặt phẳng bóng.
    /// Nhờ vậy không còn sọc, lỗ, texture bị kéo sai UV hay bounds khổng lồ.
    /// </summary>
    public partial class ShadowCaster2D : Node2D
    {
        private const string SharedMaterialPath = "res://assets/materials/world/projected_shadow_shared.tres";
        private const string ContactShadowTexturePath = "res://assets/graphics/environment/shadows/contact_ellipse_48x24_v32.png";
        private static readonly Dictionary<string, AlphaBounds> AlphaBoundsCache = new();

        [ExportGroup("Nguồn")]
        [Export]
        public NodePath SourcePath { get; set; }

        [Export]
        public Texture2D TextureOverride { get; set; }

        [Export]
        public ShadowCasterProfile Profile { get; set; }

        [Export]
        public Vector2 GroundOffset { get; set; } = Vector2.Zero;

        private CanvasItem _source;
        private Sprite2D _shadowSprite;
        private Sprite2D _contactShadowSprite;
        private ShaderMaterial _shadowMaterial;
        private Texture2D _lastTexture;
        private int _lastFrame = -1;
        private AnimatedSprite2D _connectedAnimated;
        private bool _warnedMissingSource;
        private bool _sourceRetryScheduled;
        private int _sourceResolveRetries;

        private readonly struct AlphaBounds
        {
            public AlphaBounds(Rect2 rect, bool hasPixels)
            {
                Rect = rect;
                HasPixels = hasPixels;
            }

            public Rect2 Rect { get; }
            public bool HasPixels { get; }
        }

        public override void _Ready()
        {
            EnsureShadowSprite();
            EnsureContactShadowSprite();

            Node parent = GetParent();
            if (parent != null)
            {
                parent.ChildEnteredTree += OnParentChildEnteredTree;
            }

            ResolveSource();
            SyncCaster(force: true);
            SetProcess(false);
        }

        public override void _ExitTree()
        {
            DisconnectAnimatedSource();
            Node parent = GetParent();
            if (parent != null)
            {
                parent.ChildEnteredTree -= OnParentChildEnteredTree;
            }
        }

        private void OnParentChildEnteredTree(Node child)
        {
            if (_source != null && GodotObject.IsInstanceValid(_source))
            {
                return;
            }

            ResolveSource();
            SyncCaster(force: true);
        }

        private void OnAnimatedFrameChanged()
        {
            SyncCaster(force: false);
        }

        private void ResolveSource()
        {
            DisconnectAnimatedSource();
            _source = null;

            if (SourcePath != null && !SourcePath.IsEmpty)
            {
                _source = GetNodeOrNull<CanvasItem>(SourcePath);
            }

            _source ??= FindFallbackSource();

            if (_source == null)
            {
                if (_sourceResolveRetries < 4)
                {
                    ScheduleSourceRetry();
                }
                return;
            }

            _warnedMissingSource = false;
            _sourceResolveRetries = 0;

            if (TextureOverride == null && _source is AnimatedSprite2D animated)
            {
                _connectedAnimated = animated;
                _connectedAnimated.FrameChanged += OnAnimatedFrameChanged;
            }
        }

        private void DisconnectAnimatedSource()
        {
            if (_connectedAnimated != null && GodotObject.IsInstanceValid(_connectedAnimated))
            {
                _connectedAnimated.FrameChanged -= OnAnimatedFrameChanged;
            }
            _connectedAnimated = null;
        }

        private CanvasItem FindFallbackSource()
        {
            Node parent = GetParent();
            if (parent == null)
            {
                return null;
            }

            string[] preferredNames = { "Body", "AnimatedSprite2D", "Sprite2D", "Canopy" };
            foreach (string name in preferredNames)
            {
                CanvasItem named = parent.GetNodeOrNull<CanvasItem>(name);
                if (named != null && named != this)
                {
                    return named;
                }
            }

            foreach (Node child in parent.GetChildren())
            {
                if (child == this)
                {
                    continue;
                }

                if (child is AnimatedSprite2D || child is Sprite2D)
                {
                    return child as CanvasItem;
                }
            }

            return null;
        }

        private void ScheduleSourceRetry()
        {
            if (_sourceRetryScheduled)
            {
                return;
            }

            _sourceRetryScheduled = true;
            CallDeferred(nameof(RetryResolveSource));
        }

        private void RetryResolveSource()
        {
            _sourceRetryScheduled = false;
            if (_source != null && GodotObject.IsInstanceValid(_source))
            {
                return;
            }

            _sourceResolveRetries++;
            ResolveSource();
            SyncCaster(force: true);

            if (_source != null && GodotObject.IsInstanceValid(_source))
            {
                return;
            }

            if (_sourceResolveRetries < 4)
            {
                ScheduleSourceRetry();
                return;
            }

            if (!_warnedMissingSource)
            {
                _warnedMissingSource = true;
                GD.PushWarning($"[ShadowCaster2D] Chua tim thay SourcePath tai {GetPath()}: {SourcePath}");
            }
        }

        private void EnsureShadowSprite()
        {
            if (_shadowSprite != null && GodotObject.IsInstanceValid(_shadowSprite))
            {
                return;
            }

            ShaderMaterial sharedMaterial = ResourceLoader.Load<ShaderMaterial>(SharedMaterialPath);
            if (sharedMaterial == null)
            {
                GD.PrintErr($"[ShadowCaster2D] Không load được shared material: {SharedMaterialPath}");
                return;
            }

            _shadowMaterial = sharedMaterial.Duplicate() as ShaderMaterial;
            if (_shadowMaterial == null)
            {
                GD.PrintErr($"[ShadowCaster2D] Khong duplicate duoc shared material: {SharedMaterialPath}");
                return;
            }
            _shadowMaterial.ResourceLocalToScene = true;

            _shadowSprite = new Sprite2D
            {
                Name = "ProjectedShadow",
                Material = _shadowMaterial,
                ZAsRelative = true,
                ShowBehindParent = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                // Fail-safe: nếu Godot không compile được material, mask thô vẫn hiện như một
                // bóng tối mờ thay vì silhouette TRẮNG chọc xuống dưới gốc cây. Shader hợp lệ
                // tự ghi COLOR nên SelfModulate này không đổi màu pass V3 bình thường.
                SelfModulate = new Color(0.03f, 0.05f, 0.035f, 0.26f)
            };
            AddChild(_shadowSprite);
        }

        private void EnsureContactShadowSprite()
        {
            if (_contactShadowSprite != null && GodotObject.IsInstanceValid(_contactShadowSprite))
            {
                return;
            }

            Texture2D texture = ResourceLoader.Exists(ContactShadowTexturePath)
                ? GD.Load<Texture2D>(ContactShadowTexturePath)
                : null;
            if (texture == null)
            {
                GD.PushWarning($"[ShadowCaster2D] Missing contact shadow texture: {ContactShadowTexturePath}");
                return;
            }

            _contactShadowSprite = new Sprite2D
            {
                Name = "ContactShadow",
                Texture = texture,
                Centered = true,
                ZAsRelative = true,
                ShowBehindParent = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            AddChild(_contactShadowSprite);
        }

        private void SyncCaster(bool force)
        {
            if (_shadowSprite == null || _source == null || Profile == null)
            {
                Visible = false;
                return;
            }

            Texture2D texture = TextureOverride;
            Vector2 sourcePosition = Vector2.Zero;
            Vector2 sourceScale = Vector2.One;
            float sourceRotation = 0f;
            float sourceSkew = 0f;
            Vector2 sourceOffset = Vector2.Zero;
            bool centered = true;
            bool flipH = false;
            bool flipV = false;
            bool regionEnabled = false;
            Rect2 regionRect = default;
            int frame = -1;

            if (_source is Sprite2D sprite)
            {
                texture ??= sprite.Texture;
                sourcePosition = sprite.Position;
                sourceScale = sprite.Scale;
                sourceRotation = sprite.Rotation;
                sourceSkew = sprite.Skew;
                sourceOffset = sprite.Offset;
                centered = sprite.Centered;
                flipH = sprite.FlipH;
                flipV = sprite.FlipV;
                regionEnabled = TextureOverride == null && sprite.RegionEnabled;
                regionRect = sprite.RegionRect;
            }
            else if (_source is AnimatedSprite2D animated)
            {
                frame = animated.Frame;
                if (texture == null && animated.SpriteFrames != null)
                {
                    texture = animated.SpriteFrames.GetFrameTexture(animated.Animation, animated.Frame);
                }

                sourcePosition = animated.Position;
                sourceScale = animated.Scale;
                sourceRotation = animated.Rotation;
                sourceSkew = animated.Skew;
                sourceOffset = animated.Offset;
                centered = animated.Centered;
                flipH = animated.FlipH;
                flipV = animated.FlipV;
            }
            else
            {
                Visible = false;
                return;
            }

            if (texture == null)
            {
                Visible = false;
                return;
            }

            bool textureChanged = texture != _lastTexture || frame != _lastFrame;
            _lastTexture = texture;
            _lastFrame = frame;

            if (force || textureChanged)
            {
                _shadowSprite.Texture = texture;
                _shadowSprite.RegionEnabled = regionEnabled;
                if (regionEnabled)
                {
                    _shadowSprite.RegionRect = regionRect;
                }
            }

            // Không phóng quad, không canvas padding. Proxy giữ transform y hệt source.
            _shadowSprite.Position = sourcePosition + GroundOffset;
            _shadowSprite.Scale = sourceScale;
            _shadowSprite.Rotation = sourceRotation;
            _shadowSprite.Skew = sourceSkew;
            _shadowSprite.Offset = sourceOffset;
            _shadowSprite.Centered = centered;
            _shadowSprite.FlipH = flipH;
            _shadowSprite.FlipV = flipV;
            _shadowSprite.ZIndex = Profile.ZIndex;

            Vector2 sourceSize = regionEnabled ? regionRect.Size : texture.GetSize();
            sourceSize.X = Mathf.Max(sourceSize.X, 1f);
            sourceSize.Y = Mathf.Max(sourceSize.Y, 1f);

            float left = centered ? -sourceSize.X * 0.5f : 0f;
            float top = centered ? -sourceSize.Y * 0.5f : 0f;
            AlphaBounds alphaBounds = ResolveAlphaBounds(
                texture,
                regionEnabled,
                regionRect,
                Mathf.Clamp(Profile.AlphaCutoff, 0f, 1f),
                sourceSize);

            Rect2 visibleRect = alphaBounds.HasPixels
                ? alphaBounds.Rect
                : new Rect2(Vector2.Zero, sourceSize);
            float visibleWidth = Mathf.Max(visibleRect.Size.X, 1f);
            float visibleHeight = Mathf.Max(visibleRect.Size.Y, 1f);

            float visibleLeft = left + visibleRect.Position.X + sourceOffset.X;
            float visibleTop = top + visibleRect.Position.Y + sourceOffset.Y;
            float baseXLocal = visibleLeft + visibleWidth * 0.5f;
            float baseYLocal = visibleTop + visibleHeight * Mathf.Clamp(Profile.BaseY01, 0f, 1f);
            float heightLocal = Mathf.Max(visibleHeight * Mathf.Max(Profile.HeightRatio, 0.02f), 1f);

            SetInstance("caster_projection_model", (float)Profile.Model);
            SetInstance("caster_base_x_local", baseXLocal);
            SetInstance("caster_base_y_local", baseYLocal);
            SetInstance("caster_height_local", heightLocal);
            SetInstance("caster_width_scale", Mathf.Clamp(Profile.WidthScale, 0.1f, 1.5f));
            SetInstance("caster_noon_length_world", Mathf.Max(Profile.NoonLengthWorld, 0f));
            SetInstance("caster_max_length_world", Mathf.Max(Profile.MaxLengthWorld, Profile.NoonLengthWorld));
            SetInstance("caster_noon_flatten", Mathf.Clamp(Profile.NoonFlatten, 0.01f, 0.8f));
            SetInstance("caster_horizon_flatten", Mathf.Clamp(Profile.HorizonFlatten, 0.01f, 0.8f));
            SetInstance("caster_tint", Profile.Tint);
            SetInstance("caster_opacity", Mathf.Clamp(Profile.Opacity, 0f, 1f));
            SetInstance("caster_alpha_cutoff", Mathf.Clamp(Profile.AlphaCutoff, 0f, 1f));

            SyncContactShadow(
                baseXLocal,
                baseYLocal,
                visibleWidth,
                visibleHeight,
                sourceScale);

            Visible = _source.Visible;
        }

        private void SyncContactShadow(
            float baseXLocal,
            float baseYLocal,
            float visibleWidth,
            float visibleHeight,
            Vector2 sourceScale)
        {
            if (_contactShadowSprite == null || Profile == null)
            {
                return;
            }

            bool enabled = Profile.ContactShadowEnabled && Profile.ContactOpacity > 0.001f;
            _contactShadowSprite.Visible = enabled;
            if (!enabled)
            {
                return;
            }

            // Contact shadow bám đúng alpha-base của caster, nhưng luôn nằm trên mặt đất.
            // Vì vậy nó KHÔNG kế thừa phép affine projection của bóng mặt trời.
            Vector2 anchorInShadowLocal = new(baseXLocal, baseYLocal);
            Vector2 anchorGlobal = _shadowSprite.ToGlobal(anchorInShadowLocal);
            Vector2 anchorHere = ToLocal(anchorGlobal) + Profile.ContactOffset;
            _contactShadowSprite.Position = anchorHere;
            _contactShadowSprite.Rotation = 0f;
            _contactShadowSprite.Skew = 0f;

            float desiredWidth = Mathf.Max(
                visibleWidth * Mathf.Abs(sourceScale.X) * Profile.ContactWidthRatio,
                2f);
            float desiredDepth = Mathf.Max(
                visibleHeight * Mathf.Abs(sourceScale.Y) * Profile.ContactDepthRatio,
                1.5f);

            Vector2 textureSize = _contactShadowSprite.Texture?.GetSize() ?? new Vector2(32f, 16f);
            _contactShadowSprite.Scale = new Vector2(
                desiredWidth / Mathf.Max(textureSize.X, 1f),
                desiredDepth / Mathf.Max(textureSize.Y, 1f));
            _contactShadowSprite.ZIndex = Profile.ZIndex - 1;

            Color tint = Profile.ContactTint;
            _contactShadowSprite.Modulate = new Color(
                tint.R,
                tint.G,
                tint.B,
                Mathf.Clamp(Profile.ContactOpacity, 0f, 1f));
        }

        private void SetInstance(string name, Variant value)
        {
            _shadowMaterial?.SetShaderParameter(name, value);
        }

        private static AlphaBounds ResolveAlphaBounds(
            Texture2D texture,
            bool regionEnabled,
            Rect2 regionRect,
            float alphaCutoff,
            Vector2 fallbackSize)
        {
            if (texture == null)
            {
                return new AlphaBounds(new Rect2(Vector2.Zero, fallbackSize), false);
            }

            Rect2 sampleRect = new Rect2(Vector2.Zero, fallbackSize);
            Image image = null;

            if (texture is AtlasTexture atlasTexture)
            {
                image = atlasTexture.Atlas?.GetImage();
                sampleRect = atlasTexture.Region;
            }
            else
            {
                image = texture.GetImage();
                if (regionEnabled)
                {
                    sampleRect = regionRect;
                }
            }

            if (image == null || image.IsEmpty())
            {
                return new AlphaBounds(new Rect2(Vector2.Zero, fallbackSize), false);
            }

            int imageWidth = image.GetWidth();
            int imageHeight = image.GetHeight();
            int startX = Mathf.Clamp(Mathf.FloorToInt(sampleRect.Position.X), 0, imageWidth);
            int startY = Mathf.Clamp(Mathf.FloorToInt(sampleRect.Position.Y), 0, imageHeight);
            int endX = Mathf.Clamp(Mathf.CeilToInt(sampleRect.End.X), 0, imageWidth);
            int endY = Mathf.Clamp(Mathf.CeilToInt(sampleRect.End.Y), 0, imageHeight);

            if (endX <= startX || endY <= startY)
            {
                return new AlphaBounds(new Rect2(Vector2.Zero, fallbackSize), false);
            }

            int cutoffKey = Mathf.Clamp(Mathf.RoundToInt(alphaCutoff * 255f), 0, 255);
            string cacheKey =
                $"{texture.GetInstanceId()}:{startX},{startY},{endX},{endY}:{cutoffKey}";
            if (AlphaBoundsCache.TryGetValue(cacheKey, out AlphaBounds cached))
            {
                return cached;
            }

            float cutoff = cutoffKey / 255f;
            int minX = endX;
            int minY = endY;
            int maxX = startX - 1;
            int maxY = startY - 1;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if (image.GetPixel(x, y).A <= cutoff)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }
                    if (y < minY)
                    {
                        minY = y;
                    }
                    if (x > maxX)
                    {
                        maxX = x;
                    }
                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            AlphaBounds result;
            if (maxX < minX || maxY < minY)
            {
                result = new AlphaBounds(new Rect2(Vector2.Zero, fallbackSize), false);
            }
            else
            {
                result = new AlphaBounds(
                    new Rect2(
                        new Vector2(minX - startX, minY - startY),
                        new Vector2(maxX - minX + 1, maxY - minY + 1)),
                    true);
            }

            AlphaBoundsCache[cacheKey] = result;
            return result;
        }
    }
}
