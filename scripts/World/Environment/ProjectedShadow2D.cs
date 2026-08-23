using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Bóng đổ sprite-based cho game top-down pixel art.
    ///
    /// V1.5 bỏ hoàn toàn blob/ellipse của V1.4. Node này lấy texture/frame hiện tại của
    /// chính Sprite2D/AnimatedSprite2D nguồn, tô màu tối rồi dùng shader ép + kéo silhouette
    /// theo hướng mặt trời/mặt trăng. Nhờ vậy hình bóng luôn có quan hệ trực tiếp với asset thật.
    ///
    /// Thiết kế này dùng được cho cây, đá, hoa, cỏ, nhân vật và cả layer cliff/wall.
    /// </summary>
    public partial class ProjectedShadow2D : Node2D
    {
        public enum ShadowProjectionMode
        {
            /// <summary>Ép sprite xuống mặt đất, phù hợp prop đứng: cây/đá/hoa/nhân vật.</summary>
            GroundPlane = 0,

            /// <summary>Dịch nguyên silhouette, phù hợp layer lớn như cliff wall.</summary>
            RigidDrop = 1
        }

        private const string ShadowShaderPath = "res://assets/shaders/world/projected_asset_shadow.gdshader";

        [ExportGroup("Nguồn hình")]
        [Export]
        public NodePath SourcePath { get; set; }

        /// <summary>
        /// Texture override dùng khi visual thật đã bị tách thành trunk/canopy nhưng ta muốn bóng
        /// lấy silhouette của asset nguyên bản. Nếu null, texture/frame của SourcePath được dùng.
        /// </summary>
        [Export]
        public Texture2D TextureOverride { get; set; }

        [Export]
        public ShadowProjectionMode ProjectionMode { get; set; } = ShadowProjectionMode.GroundPlane;

        [ExportGroup("Điểm tiếp đất")]
        /// <summary>
        /// Tỉ lệ Y của chân asset trong texture (0 = đỉnh, 1 = đáy).
        /// Dùng alpha-bounds của asset để canh chính xác thay vì đoán theo full PNG trong suốt.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.001")]
        public float BaseY01 { get; set; } = 0.92f;

        /// <summary>Dịch shadow proxy trong local-space của parent để chân bóng khớp mặt đất.</summary>
        [Export]
        public Vector2 GroundOffset { get; set; } = Vector2.Zero;

        [ExportGroup("Projection")]
        /// <summary>Chiều cao giả, theo pixel local của sprite nguồn.</summary>
        [Export(PropertyHint.Range, "1,2048,1")]
        public float VirtualHeightPixels { get; set; } = 32f;

        /// <summary>Độ dài bóng tối thiểu khi mặt trời ở cao nhất.</summary>
        [Export(PropertyHint.Range, "0,128,0.25")]
        public float NoonProjectionPixels { get; set; } = 1.5f;

        /// <summary>Clamp chiều dài bóng khi thiên thể gần đường chân trời.</summary>
        [Export(PropertyHint.Range, "1,1024,1")]
        public float MaxProjectionPixels { get; set; } = 48f;

        /// <summary>Độ dẹt lúc trưa. Giá trị nhỏ = nằm sát mặt đất hơn.</summary>
        [Export(PropertyHint.Range, "0.02,1,0.01")]
        public float NoonFlatten { get; set; } = 0.16f;

        /// <summary>Độ dẹt lúc sáng/chiều. Cho bóng dài vẫn còn đọc được silhouette.</summary>
        [Export(PropertyHint.Range, "0.02,1,0.01")]
        public float HorizonFlatten { get; set; } = 0.24f;

        [ExportGroup("Màu / alpha")]
        [Export]
        public Color ShadowTint { get; set; } = new Color(0.045f, 0.070f, 0.040f, 1f);

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float BaseOpacity { get; set; } = 0.40f;

        /// <summary>Loại alpha nền mờ/halo của asset tải ngoài.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float AlphaCutoff { get; set; } = 0.08f;

        [ExportGroup("Render")]
        [Export]
        public int ShadowZIndex { get; set; } = -1;

        private CanvasItem _source;
        private Sprite2D _shadowSprite;
        private ShaderMaterial _shadowMaterial;
        private Texture2D _lastTexture;
        private bool _ready;
        private bool _reportedMissingSource;

        public override void _Ready()
        {
            ResolveSource();
            EnsureShadowVisual();
            SyncSourceVisual(forceTexture: true);
            SetProcess(false);
        }

        /// <summary>
        /// Được EnvironmentShadowBus gọi theo nhịp chung. Không có _Process riêng cho từng prop,
        /// nên Field 1 có hàng trăm cây/cỏ vẫn không biến thành lễ hội script update.
        /// </summary>
        public void ApplyEnvironment(EnvironmentState state)
        {
            if (state == null)
            {
                return;
            }

            if (!_ready)
            {
                ResolveSource();
                EnsureShadowVisual();
            }

            // Player dựng Body deferred từ CharacterConfig, vì vậy source có thể chưa tồn tại ở _Ready.
            // Thử resolve lại ở nhịp ShadowBus thay vì bắt mọi prefab phải có visual ngay lập tức.
            if (_source == null || !GodotObject.IsInstanceValid(_source))
            {
                ResolveSource();
            }

            if (_source == null || !GodotObject.IsInstanceValid(_source) || _shadowSprite == null)
            {
                Visible = false;
                return;
            }

            SyncSourceVisual(forceTexture: false);

            Vector2 direction = state.KeyLightDirection.LengthSquared() > 0.0001f
                ? state.KeyLightDirection.Normalized()
                : Vector2.Down;

            float elevation = Mathf.Clamp(state.KeyLightElevation, 0f, 1f);
            float lowSun = 1f - elevation;
            float keyStrength = Mathf.Clamp(state.KeyLightStrength01, 0f, 1f);

            // Khi mặt trời thấp: bóng dài nhanh hơn, nhưng clamp để không lặp lại "xúc xích bóng" V1.4.
            float projection = Mathf.Lerp(
                Mathf.Max(NoonProjectionPixels, 0f),
                Mathf.Max(MaxProjectionPixels, NoonProjectionPixels),
                Mathf.Pow(lowSun, 0.72f));

            float flatten = Mathf.Lerp(
                Mathf.Clamp(NoonFlatten, 0.02f, 1f),
                Mathf.Clamp(HorizonFlatten, 0.02f, 1f),
                lowSun);

            // Mây làm direct shadow mềm/yếu hơn. Ban đêm moon shadow vẫn còn rất nhẹ nếu moon đủ mạnh.
            float cloudAttenuation = Mathf.Lerp(1f, 0.52f, Mathf.Clamp(state.Cloudiness, 0f, 1f));
            float lightResponse = Mathf.Lerp(0.18f, 1f, keyStrength);
            float alpha = BaseOpacity
                * Mathf.Clamp(state.ShadowStrength, 0f, 1f)
                * cloudAttenuation
                * lightResponse;

            Color nightTint = new Color(0.040f, 0.050f, 0.085f, 1f);
            Color tint = ShadowTint.Lerp(nightTint, Mathf.Clamp(state.NightFactor * 0.42f, 0f, 0.42f));
            tint.A = Mathf.Clamp(alpha, 0f, 0.72f);

            _shadowMaterial.SetShaderParameter("shadow_direction", direction);
            _shadowMaterial.SetShaderParameter("projection_pixels", projection);
            _shadowMaterial.SetShaderParameter("flatten_factor", flatten);
            _shadowMaterial.SetShaderParameter("virtual_height_pixels", Mathf.Max(VirtualHeightPixels, 1f));
            _shadowMaterial.SetShaderParameter("projection_mode", (float)ProjectionMode);
            _shadowMaterial.SetShaderParameter("shadow_color", tint);
            _shadowMaterial.SetShaderParameter("alpha_cutoff", Mathf.Clamp(AlphaCutoff, 0f, 1f));

            Visible = _source.Visible && tint.A > 0.008f;
        }

        private void ResolveSource()
        {
            _source = null;
            if (SourcePath != null && !SourcePath.IsEmpty)
            {
                _source = GetNodeOrNull<CanvasItem>(SourcePath);
            }

            if (_source == null)
            {
                if (!_reportedMissingSource)
                {
                    _reportedMissingSource = true;
                    GD.PushWarning($"[ProjectedShadow2D] Chưa tìm thấy SourcePath tại {GetPath()}: {SourcePath}");
                }
            }
            else
            {
                _reportedMissingSource = false;
            }
        }

        private void EnsureShadowVisual()
        {
            if (_shadowSprite == null || !GodotObject.IsInstanceValid(_shadowSprite))
            {
                _shadowSprite = new Sprite2D
                {
                    Name = "ShadowSilhouette",
                    ZIndex = ShadowZIndex,
                    ZAsRelative = true,
                    ShowBehindParent = true,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest
                };
                AddChild(_shadowSprite);
            }

            if (_shadowMaterial == null || !GodotObject.IsInstanceValid(_shadowMaterial))
            {
                Shader shader = ResourceLoader.Load<Shader>(ShadowShaderPath);
                if (shader == null)
                {
                    GD.PrintErr($"[ProjectedShadow2D] Không load được shader: {ShadowShaderPath}");
                    _ready = false;
                    return;
                }

                // Mỗi caster có geometry/tuning riêng nên material là local cho caster.
                // EnvironmentShadowBus vẫn update chúng theo một nhịp chung, không có _Process riêng.
                _shadowMaterial = new ShaderMaterial { Shader = shader };
                _shadowSprite.Material = _shadowMaterial;
            }

            _ready = true;
        }

        private void SyncSourceVisual(bool forceTexture)
        {
            if (_source == null || _shadowSprite == null || _shadowMaterial == null)
            {
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
                GD.PushWarning($"[ProjectedShadow2D] Source phải là Sprite2D/AnimatedSprite2D: {_source.GetPath()}");
                Visible = false;
                return;
            }

            if (texture == null)
            {
                Visible = false;
                return;
            }

            if (forceTexture || texture != _lastTexture)
            {
                _lastTexture = texture;
                _shadowSprite.Texture = texture;
            }

            // Component được đặt cùng parent với source trong các prefab của Ashes.
            // Copy transform giúp shadow giữ đúng kích thước asset gốc; GroundOffset chỉ canh chân xuống đất.
            _shadowSprite.Position = sourcePosition + GroundOffset;
            _shadowSprite.Scale = sourceScale;
            _shadowSprite.Rotation = sourceRotation;
            _shadowSprite.Skew = sourceSkew;
            _shadowSprite.Offset = sourceOffset;
            _shadowSprite.Centered = centered;
            _shadowSprite.FlipH = flipH;
            _shadowSprite.FlipV = flipV;
            _shadowSprite.RegionEnabled = regionEnabled;
            if (regionEnabled)
            {
                _shadowSprite.RegionRect = regionRect;
            }

            Vector2 visualSize = regionEnabled ? regionRect.Size : texture.GetSize();
            float topY = centered ? -visualSize.Y * 0.5f : 0f;
            float baseY = topY + visualSize.Y * Mathf.Clamp(BaseY01, 0f, 1f) + sourceOffset.Y;
            _shadowMaterial.SetShaderParameter("base_y_pixels", baseY);
        }
    }
}
