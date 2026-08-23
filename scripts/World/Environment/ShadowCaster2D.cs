using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Shadow caster V5.0: chỉ tạo footprint Sprite2D trên ground plane.
    ///
    /// V4 dùng shader để biến đổi silhouette rồi phải thêm mass-shadow để che khuyết điểm.
    /// V5 bỏ hẳn phép chiếu vertex. Một caster chỉ có:
    /// - Ground anchor.
    /// - Footprint texture.
    /// - Width/length/opacity từ profile + EnvironmentState.
    /// - Contact AO nhỏ sát chân.
    ///
    /// TextureOverride giờ mang nghĩa đúng: footprint art riêng của caster (nếu có).
    /// Nếu null, hệ thống dùng footprint mềm chung cho actor/rock/flora.
    /// </summary>
    public partial class ShadowCaster2D : Node2D
    {
        public const string RuntimeGroup = "EnvironmentShadowCasterV5";

        private const string GenericFootprintPath = "res://assets/graphics/environment/shadows/v5/soft_footprint_v5.png";
        private const string ContactShadowPath = "res://assets/graphics/environment/shadows/v5/contact_ao_v5.png";

        private static readonly Dictionary<string, AlphaBounds> AlphaBoundsCache = new();

        [ExportGroup("Nguồn")]
        [Export]
        public NodePath SourcePath { get; set; }

        /// <summary>
        /// V5: texture footprint nằm sẵn trên ground plane. Tree/apple tree có footprint riêng.
        /// Actor/rock có thể để null để dùng soft footprint chung.
        /// </summary>
        [Export]
        public Texture2D TextureOverride { get; set; }

        [Export]
        public ShadowCasterProfile Profile { get; set; }

        /// <summary>
        /// Offset từ đáy alpha của source tới điểm chạm đất thực, tính trong local space của caster.
        /// Với tree split-canopy, offset này bù từ đáy canopy xuống chân trunk.
        /// </summary>
        [Export]
        public Vector2 GroundOffset { get; set; } = Vector2.Zero;

        private CanvasItem _source;
        private Sprite2D _projectedShadow;
        private Sprite2D _contactShadow;
        private Texture2D _genericFootprint;
        private Texture2D _contactTexture;
        private AnimatedSprite2D _connectedAnimated;

        private Vector2 _groundAnchorLocal;
        private float _visibleWidthWorld = 24f;
        private float _visibleHeightWorld = 24f;
        private bool _geometryReady;
        private bool _warnedSource;

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

        public override void _EnterTree()
        {
            AddToGroup(RuntimeGroup, false);
        }

        public override void _Ready()
        {
            _genericFootprint = ResourceLoader.Exists(GenericFootprintPath)
                ? GD.Load<Texture2D>(GenericFootprintPath)
                : null;
            _contactTexture = ResourceLoader.Exists(ContactShadowPath)
                ? GD.Load<Texture2D>(ContactShadowPath)
                : null;

            EnsureSprites();
            ResolveSource();
            SyncGeometry();
            SetProcess(false);
        }

        public override void _ExitTree()
        {
            DisconnectAnimatedSource();
        }

        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null || Profile == null)
            {
                return;
            }

            if (_source == null || !GodotObject.IsInstanceValid(_source))
            {
                ResolveSource();
                SyncGeometry();
            }

            if (!_geometryReady || _projectedShadow == null)
            {
                return;
            }

            if (Profile.Model == ShadowCasterProfile.ProjectionModel.RigidDrop)
            {
                ApplyRigidDrop(state);
                return;
            }

            ApplyFootprint(state);
        }

        private void EnsureSprites()
        {
            if (_projectedShadow == null)
            {
                _projectedShadow = new Sprite2D
                {
                    Name = "ProjectedShadowV5",
                    Centered = true,
                    ZAsRelative = true,
                    ShowBehindParent = true,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear
                };
                AddChild(_projectedShadow);
            }

            if (_contactShadow == null)
            {
                _contactShadow = new Sprite2D
                {
                    Name = "ContactShadowV5",
                    Centered = true,
                    ZAsRelative = true,
                    ShowBehindParent = true,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                    Texture = _contactTexture
                };
                AddChild(_contactShadow);
            }
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
                if (!_warnedSource)
                {
                    _warnedSource = true;
                    GD.PushWarning($"[ShadowCaster2D V5] Không tìm thấy source tại {GetPath()} | SourcePath={SourcePath}");
                }
                return;
            }

            _warnedSource = false;
            if (_source is AnimatedSprite2D animated)
            {
                _connectedAnimated = animated;
                _connectedAnimated.FrameChanged += OnAnimatedFrameChanged;
            }
        }

        private CanvasItem FindFallbackSource()
        {
            Node parent = GetParent();
            if (parent == null)
            {
                return null;
            }

            string[] preferred = { "Body", "AnimatedSprite2D", "Sprite2D", "Canopy", "Trunk" };
            foreach (string name in preferred)
            {
                if (parent.GetNodeOrNull<CanvasItem>(name) is CanvasItem item && item != this)
                {
                    return item;
                }
            }

            foreach (Node child in parent.GetChildren())
            {
                if (child != this && (child is Sprite2D || child is AnimatedSprite2D))
                {
                    return child as CanvasItem;
                }
            }

            return null;
        }

        private void DisconnectAnimatedSource()
        {
            if (_connectedAnimated != null && GodotObject.IsInstanceValid(_connectedAnimated))
            {
                _connectedAnimated.FrameChanged -= OnAnimatedFrameChanged;
            }
            _connectedAnimated = null;
        }

        private void OnAnimatedFrameChanged()
        {
            SyncGeometry();
        }

        private void SyncGeometry()
        {
            _geometryReady = false;
            if (_source == null || Profile == null || _projectedShadow == null)
            {
                Visible = false;
                return;
            }

            Texture2D sourceTexture = ResolveSourceTexture();
            if (sourceTexture == null)
            {
                Visible = false;
                return;
            }

            SourceVisual visual = ResolveSourceVisual(sourceTexture);
            Vector2 anchorGlobal = visual.SourceNode.ToGlobal(visual.BottomCenterLocal);
            Vector2 groundDeltaGlobal = ToGlobal(GroundOffset) - ToGlobal(Vector2.Zero);
            anchorGlobal += groundDeltaGlobal;
            _groundAnchorLocal = ToLocal(anchorGlobal);

            Vector2 leftGlobal = visual.SourceNode.ToGlobal(visual.LeftCenterLocal);
            Vector2 rightGlobal = visual.SourceNode.ToGlobal(visual.RightCenterLocal);
            Vector2 topGlobal = visual.SourceNode.ToGlobal(visual.TopCenterLocal);
            Vector2 bottomGlobal = visual.SourceNode.ToGlobal(visual.BottomCenterLocal);
            _visibleWidthWorld = Mathf.Max(leftGlobal.DistanceTo(rightGlobal), 2f);
            _visibleHeightWorld = Mathf.Max(topGlobal.DistanceTo(bottomGlobal), 2f);

            if (Profile.Model == ShadowCasterProfile.ProjectionModel.RigidDrop)
            {
                ConfigureRigidDropSprite(sourceTexture, visual);
            }
            else
            {
                _projectedShadow.Texture = TextureOverride ?? _genericFootprint;
                _projectedShadow.Centered = true;
                _projectedShadow.RegionEnabled = false;
                _projectedShadow.Offset = Vector2.Zero;
                _projectedShadow.FlipH = false;
                _projectedShadow.FlipV = false;
            }

            SyncContactShadow();
            _projectedShadow.ZIndex = Profile.ZIndex;
            Visible = _source.Visible;
            _geometryReady = _projectedShadow.Texture != null;
        }

        private void ApplyFootprint(EnvironmentState state)
        {
            Vector2 direction = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;
            float length01 = Mathf.Clamp(state.ShadowLength01, 0f, 1f);
            float lengthCurve = Mathf.Pow(length01, 0.84f);

            float castLengthWorld = Mathf.Lerp(
                Mathf.Max(Profile.NoonLengthWorld, 1f),
                Mathf.Max(Profile.MaxLengthWorld, Profile.NoonLengthWorld),
                lengthCurve);

            float modelWidthFactor = Profile.Model switch
            {
                ShadowCasterProfile.ProjectionModel.ArtDirectedFootprint => Mathf.Lerp(0.70f, 0.92f, lengthCurve),
                ShadowCasterProfile.ProjectionModel.Volume => Mathf.Lerp(0.72f, 0.86f, lengthCurve),
                _ => Mathf.Lerp(0.52f, 0.68f, lengthCurve)
            };

            float widthWorld = Mathf.Max(_visibleWidthWorld * Profile.WidthScale * modelWidthFactor, 2.5f);
            Vector2 anchorGlobal = ToGlobal(_groundAnchorLocal);
            Vector2 centerGlobal = anchorGlobal + direction * (castLengthWorld * 0.50f);
            Vector2 centerLocal = ToLocal(centerGlobal);

            Vector2 localDir = ToLocal(anchorGlobal + direction) - ToLocal(anchorGlobal);
            if (localDir.LengthSquared() < 0.0001f)
            {
                localDir = Vector2.Down;
            }
            localDir = localDir.Normalized();

            Vector2 side = new(-direction.Y, direction.X);
            float localLength = (ToLocal(anchorGlobal + direction * castLengthWorld) - _groundAnchorLocal).Length();
            float localWidth = (ToLocal(anchorGlobal + side * widthWorld) - _groundAnchorLocal).Length();

            Vector2 texSize = _projectedShadow.Texture?.GetSize() ?? new Vector2(64f, 64f);
            _projectedShadow.Position = centerLocal;
            _projectedShadow.Rotation = localDir.Angle() - Mathf.Pi * 0.5f;
            _projectedShadow.Scale = new Vector2(
                localWidth / Mathf.Max(texSize.X, 1f),
                localLength / Mathf.Max(texSize.Y, 1f));

            float keyVisibility = 0.42f + 0.58f * Mathf.Sqrt(Mathf.Clamp(state.KeyLightStrength01, 0f, 1f));
            float cloudAttenuation = 1f - Mathf.Clamp(state.Cloudiness, 0f, 1f) * 0.24f;
            float nightAttenuation = Mathf.Lerp(1f, 0.62f, Mathf.Clamp(state.NightFactor, 0f, 1f));
            float horizonBoost = Mathf.Lerp(0.90f, 1.08f, lengthCurve);
            float alpha = Profile.Opacity
                * Mathf.Clamp(state.ShadowStrength, 0f, 1f)
                * keyVisibility
                * cloudAttenuation
                * nightAttenuation
                * horizonBoost;

            Color nightTint = new(0.020f, 0.032f, 0.060f, 1f);
            Color tint = Profile.Tint.Lerp(nightTint, Mathf.Clamp(state.NightFactor * 0.44f, 0f, 0.44f));
            _projectedShadow.Modulate = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(alpha, 0f, 0.56f));
            _projectedShadow.Visible = _source.Visible;
        }

        private void ApplyRigidDrop(EnvironmentState state)
        {
            if (_source is not Node2D sourceNode || _projectedShadow.Texture == null)
            {
                return;
            }

            Vector2 direction = state.ShadowDirection2D.LengthSquared() > 0.0001f
                ? state.ShadowDirection2D.Normalized()
                : Vector2.Down;
            float lengthCurve = Mathf.Pow(Mathf.Clamp(state.ShadowLength01, 0f, 1f), 0.84f);
            float distanceWorld = Mathf.Lerp(Profile.NoonLengthWorld, Profile.MaxLengthWorld, lengthCurve);
            Vector2 offsetLocal = ToLocal(GlobalPosition + direction * distanceWorld) - ToLocal(GlobalPosition);

            _projectedShadow.Position = ToLocal(sourceNode.GlobalPosition) + offsetLocal;
            _projectedShadow.Rotation = sourceNode.GlobalRotation - GlobalRotation;
            Vector2 myScale = GlobalScale;
            Vector2 sourceScale = sourceNode.GlobalScale;
            _projectedShadow.Scale = new Vector2(
                sourceScale.X / Mathf.Max(Mathf.Abs(myScale.X), 0.0001f),
                sourceScale.Y / Mathf.Max(Mathf.Abs(myScale.Y), 0.0001f));

            float alpha = Profile.Opacity
                * Mathf.Clamp(state.ShadowStrength, 0f, 1f)
                * Mathf.Lerp(0.86f, 1.0f, lengthCurve)
                * Mathf.Lerp(1f, 0.60f, state.NightFactor);
            Color tint = Profile.Tint.Lerp(new Color(0.020f, 0.032f, 0.060f, 1f), state.NightFactor * 0.40f);
            _projectedShadow.Modulate = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(alpha, 0f, 0.42f));
            _projectedShadow.Visible = _source.Visible;
        }

        private void ConfigureRigidDropSprite(Texture2D texture, SourceVisual visual)
        {
            _projectedShadow.Texture = texture;
            _projectedShadow.Centered = visual.Centered;
            _projectedShadow.Offset = visual.Offset;
            _projectedShadow.FlipH = visual.FlipH;
            _projectedShadow.FlipV = visual.FlipV;
            _projectedShadow.RegionEnabled = visual.RegionEnabled;
            if (visual.RegionEnabled)
            {
                _projectedShadow.RegionRect = visual.RegionRect;
            }
        }

        private void SyncContactShadow()
        {
            if (_contactShadow == null || Profile == null)
            {
                return;
            }

            bool enabled = Profile.ContactShadowEnabled
                && Profile.ContactOpacity > 0.001f
                && _contactTexture != null;
            _contactShadow.Visible = enabled;
            if (!enabled)
            {
                return;
            }

            Vector2 anchorGlobal = ToGlobal(_groundAnchorLocal);
            Vector2 contactOffsetGlobal = ToGlobal(Profile.ContactOffset) - ToGlobal(Vector2.Zero);
            Vector2 positionLocal = ToLocal(anchorGlobal + contactOffsetGlobal);

            float widthWorld = Mathf.Max(_visibleWidthWorld * Profile.ContactWidthRatio, 2f);
            float depthWorld = Mathf.Max(_visibleHeightWorld * Profile.ContactDepthRatio, 1.5f);
            Vector2 localX = ToLocal(anchorGlobal + Vector2.Right * widthWorld) - _groundAnchorLocal;
            Vector2 localY = ToLocal(anchorGlobal + Vector2.Down * depthWorld) - _groundAnchorLocal;
            Vector2 texSize = _contactTexture.GetSize();

            _contactShadow.Position = positionLocal;
            _contactShadow.Rotation = -GlobalRotation;
            _contactShadow.Scale = new Vector2(
                localX.Length() / Mathf.Max(texSize.X, 1f),
                localY.Length() / Mathf.Max(texSize.Y, 1f));
            _contactShadow.ZIndex = Profile.ZIndex - 1;
            Color tint = Profile.ContactTint;
            _contactShadow.Modulate = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(Profile.ContactOpacity, 0f, 1f));
        }

        private Texture2D ResolveSourceTexture()
        {
            if (_source is Sprite2D sprite)
            {
                return sprite.Texture;
            }

            if (_source is AnimatedSprite2D animated && animated.SpriteFrames != null)
            {
                return animated.SpriteFrames.GetFrameTexture(animated.Animation, animated.Frame);
            }

            return null;
        }

        private readonly struct SourceVisual
        {
            public SourceVisual(
                Node2D sourceNode,
                Vector2 leftCenterLocal,
                Vector2 rightCenterLocal,
                Vector2 topCenterLocal,
                Vector2 bottomCenterLocal,
                bool centered,
                Vector2 offset,
                bool flipH,
                bool flipV,
                bool regionEnabled,
                Rect2 regionRect)
            {
                SourceNode = sourceNode;
                LeftCenterLocal = leftCenterLocal;
                RightCenterLocal = rightCenterLocal;
                TopCenterLocal = topCenterLocal;
                BottomCenterLocal = bottomCenterLocal;
                Centered = centered;
                Offset = offset;
                FlipH = flipH;
                FlipV = flipV;
                RegionEnabled = regionEnabled;
                RegionRect = regionRect;
            }

            public Node2D SourceNode { get; }
            public Vector2 LeftCenterLocal { get; }
            public Vector2 RightCenterLocal { get; }
            public Vector2 TopCenterLocal { get; }
            public Vector2 BottomCenterLocal { get; }
            public bool Centered { get; }
            public Vector2 Offset { get; }
            public bool FlipH { get; }
            public bool FlipV { get; }
            public bool RegionEnabled { get; }
            public Rect2 RegionRect { get; }
        }

        private SourceVisual ResolveSourceVisual(Texture2D texture)
        {
            bool centered = true;
            Vector2 offset = Vector2.Zero;
            bool flipH = false;
            bool flipV = false;
            bool regionEnabled = false;
            Rect2 regionRect = default;
            Node2D node = _source as Node2D;

            if (_source is Sprite2D sprite)
            {
                centered = sprite.Centered;
                offset = sprite.Offset;
                flipH = sprite.FlipH;
                flipV = sprite.FlipV;
                regionEnabled = sprite.RegionEnabled;
                regionRect = sprite.RegionRect;
            }
            else if (_source is AnimatedSprite2D animated)
            {
                centered = animated.Centered;
                offset = animated.Offset;
                flipH = animated.FlipH;
                flipV = animated.FlipV;
            }

            Vector2 size = regionEnabled ? regionRect.Size : texture.GetSize();
            size.X = Mathf.Max(size.X, 1f);
            size.Y = Mathf.Max(size.Y, 1f);
            AlphaBounds bounds = ResolveAlphaBounds(texture, regionEnabled, regionRect, Profile?.AlphaCutoff ?? 0.08f, size);
            Rect2 rect = bounds.HasPixels ? bounds.Rect : new Rect2(Vector2.Zero, size);

            float left = centered ? -size.X * 0.5f : 0f;
            float top = centered ? -size.Y * 0.5f : 0f;
            float x0 = left + rect.Position.X + offset.X;
            float x1 = x0 + rect.Size.X;
            float y0 = top + rect.Position.Y + offset.Y;
            float y1 = y0 + rect.Size.Y;
            float cx = (x0 + x1) * 0.5f;
            float cy = (y0 + y1) * 0.5f;

            return new SourceVisual(
                node,
                new Vector2(x0, cy),
                new Vector2(x1, cy),
                new Vector2(cx, y0),
                new Vector2(cx, y1),
                centered,
                offset,
                flipH,
                flipV,
                regionEnabled,
                regionRect);
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

            Rect2 sampleRect = new(Vector2.Zero, fallbackSize);
            Image image = null;
            if (texture is AtlasTexture atlas)
            {
                image = atlas.Atlas?.GetImage();
                sampleRect = atlas.Region;
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

            int startX = Mathf.Clamp(Mathf.FloorToInt(sampleRect.Position.X), 0, image.GetWidth());
            int startY = Mathf.Clamp(Mathf.FloorToInt(sampleRect.Position.Y), 0, image.GetHeight());
            int endX = Mathf.Clamp(Mathf.CeilToInt(sampleRect.End.X), 0, image.GetWidth());
            int endY = Mathf.Clamp(Mathf.CeilToInt(sampleRect.End.Y), 0, image.GetHeight());
            int cutoffKey = Mathf.Clamp(Mathf.RoundToInt(alphaCutoff * 255f), 0, 255);
            string cacheKey = $"{texture.GetInstanceId()}:{startX},{startY},{endX},{endY}:{cutoffKey}";
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
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            AlphaBounds result = maxX < minX || maxY < minY
                ? new AlphaBounds(new Rect2(Vector2.Zero, fallbackSize), false)
                : new AlphaBounds(
                    new Rect2(
                        new Vector2(minX - startX, minY - startY),
                        new Vector2(maxX - minX + 1, maxY - minY + 1)),
                    true);
            AlphaBoundsCache[cacheKey] = result;
            return result;
        }
    }
}
