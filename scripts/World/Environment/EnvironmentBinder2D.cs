using System;
using System.Collections.Generic;
using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Binder map V5.4.
    ///
    /// EnvironmentState -> GPU globals đã do WorldEnvironmentService + ShaderGlobalBridge xử lý.
    /// Binder chỉ gắn các consumer thuộc riêng scene map: CanvasModulate, lighting, shadow system,
    /// fireflies và atmosphere. Không scan ShaderMaterial nữa.
    ///
    /// V13 bổ sung tree-ground influence map: quét vị trí cây trong map, bake một mask mềm và đẩy
    /// vào ground/path shader để mặt đất thật sự phản ứng theo vị trí rễ cây.
    /// </summary>
    public partial class EnvironmentBinder2D : Node
    {
        [Export]
        public EnvironmentProfile Profile { get; set; }

        [Export]
        public NodePath CanvasModulatePath { get; set; }

        [Export]
        public NodePath VisualGroundPath { get; set; } = new NodePath("../../VisualGround");

        [Export]
        public NodePath TreeRootPath { get; set; } = new NodePath("../../Props/Trees");

        [Export]
        public float TreeGroundDarkenStrength { get; set; } = 0.172f;

        [Export]
        public float TreeGroundTintStrength { get; set; } = 0.100f;

        [Export]
        public bool TreeGroundAffectsPath { get; set; } = true;

        [Export]
        public bool ApplyField01GradePolish { get; set; } = true;

        private WorldEnvironmentService _environment;
        private CanvasModulate _canvasModulate;
        private WorldLighting2D _lighting;
        private AmbientFireflies2D _fireflies;
        private WorldAtmosphere2D _atmosphere;
        private EnvironmentShadowSystem2D _shadowSystem;
        private EnvironmentMassShadow2D _massShadow;
        private CanvasItem _sceneCloudShadow;
        private CanvasItem _sceneColorGrade;
        private bool _reportedReady;
        private bool _treeGroundApplied;

        public override void _Ready()
        {
            _environment = WorldEnvironmentService.GetOrCreate(GetTree());
            if (_environment == null)
            {
                GD.PrintErr("[EnvironmentBinder2D V5.4] WorldEnvironmentService unavailable.");
                SetProcess(false);
                return;
            }

            if (Profile != null)
            {
                _environment.SetProfile(Profile, snapToDefaultWeather: false);
            }

            if (CanvasModulatePath != null && !CanvasModulatePath.IsEmpty)
            {
                _canvasModulate = GetNodeOrNull<CanvasModulate>(CanvasModulatePath);
            }

            EnsureRuntimeFx();
            ApplyField01GradePolishIfNeeded();
            ApplyTreeGroundInfluence();
            ApplyVisualState();
        }

        public override void _Process(double delta)
        {
            ApplyVisualState();
        }

        private void EnsureRuntimeFx()
        {
            _lighting = GetNodeOrNull<WorldLighting2D>("CelestialLighting");
            if (_lighting == null)
            {
                _lighting = new WorldLighting2D { Name = "CelestialLighting" };
                AddChild(_lighting);
            }

            _shadowSystem = GetNodeOrNull<EnvironmentShadowSystem2D>("ShadowSystemV5");
            if (_shadowSystem == null)
            {
                _shadowSystem = new EnvironmentShadowSystem2D { Name = "ShadowSystemV5" };
                AddChild(_shadowSystem);
            }

            _massShadow = GetNodeOrNull<EnvironmentMassShadow2D>("MassShadowV51");
            if (_massShadow == null)
            {
                _massShadow = new EnvironmentMassShadow2D { Name = "MassShadowV51" };
                AddChild(_massShadow);
            }

            _fireflies = GetNodeOrNull<AmbientFireflies2D>("NightFireflies");
            if (_fireflies == null)
            {
                _fireflies = new AmbientFireflies2D { Name = "NightFireflies" };
                AddChild(_fireflies);
            }

            _atmosphere = GetNodeOrNull<WorldAtmosphere2D>("Atmosphere");
            if (_atmosphere == null)
            {
                _atmosphere = new WorldAtmosphere2D { Name = "Atmosphere" };
                AddChild(_atmosphere);
            }

            _sceneCloudShadow = GetNodeOrNull<CanvasItem>("../WorldCloudShadow");
            _sceneColorGrade = GetNodeOrNull<CanvasItem>("../../WorldPostFX/ColorGrade");
            if (_sceneCloudShadow != null)
            {
                _sceneCloudShadow.Visible = true;
            }
            if (_sceneColorGrade != null)
            {
                _sceneColorGrade.Visible = true;
            }

            if (!_reportedReady)
            {
                _reportedReady = true;
                GD.Print(
                    $"[EnvironmentBinder2D] READY V5.4 | gpu=global_uniforms | material_scan=OFF | lighting_owner=material | " +
                    $"grass_canvas=456x474 | shadow=ground_footprint | mass_shadow=ON | profile={Profile?.ResourcePath ?? "<none>"}");
            }
        }

        private void ApplyField01GradePolishIfNeeded()
        {
            if (!ApplyField01GradePolish)
            {
                return;
            }

            if (_sceneColorGrade?.Material is not ShaderMaterial gradeMat)
            {
                return;
            }

            // V14 lighting polish: push evening warmer, keep noon clean, and reduce the cyan wash at night.
            gradeMat.SetShaderParameter("contrast", 1.018f);
            gradeMat.SetShaderParameter("day_saturation", 0.90f);
            gradeMat.SetShaderParameter("golden_saturation", 1.035f);
            gradeMat.SetShaderParameter("night_saturation", 0.89f);
            gradeMat.SetShaderParameter("golden_strength", 0.215f);
            gradeMat.SetShaderParameter("night_blue_strength", 0.46f);
            gradeMat.SetShaderParameter("night_exposure", 0.125f);
            gradeMat.SetShaderParameter("green_tame", 0.205f);
            gradeMat.SetShaderParameter("vignette_strength", 0.018f);
            gradeMat.SetShaderParameter("cohesion_strength", 0.13f);
            gradeMat.SetShaderParameter("cohesion_tint", new Color(0.947f, 0.987f, 0.875f));

            GD.Print("[EnvironmentBinder2D] FIELD01_GRADE_V14 | noon=clean | golden=warmer | night=less_cyan");
        }

        private void ApplyVisualState()
        {
            if (_environment == null)
            {
                return;
            }

            EnvironmentState state = _environment.CurrentState;
            _lighting?.ApplyEnvironment(state);
            _shadowSystem?.ApplyEnvironment(state);
            _massShadow?.ApplyEnvironment(state);
            _fireflies?.ApplyEnvironment(state);
            _atmosphere?.ApplyEnvironment(state);

            if (_canvasModulate != null)
            {
                _canvasModulate.Color = state.AmbientColor;
            }
        }

        private void ApplyTreeGroundInfluence()
        {
            if (_treeGroundApplied)
            {
                return;
            }

            Node2D visualGround = GetNodeOrNull<Node2D>(VisualGroundPath);
            Node treeRoot = GetNodeOrNull(TreeRootPath);
            if (visualGround == null || treeRoot == null)
            {
                GD.PrintErr($"[EnvironmentBinder2D V14] Missing VisualGround ({VisualGroundPath}) or tree root ({TreeRootPath}).");
                return;
            }

            Sprite2D groundBase = visualGround.GetNodeOrNull<Sprite2D>("GroundBase");
            if (groundBase?.Texture == null)
            {
                GD.PrintErr("[EnvironmentBinder2D V14] GroundBase missing texture; tree-ground influence skipped.");
                return;
            }

            Vector2 textureSize = groundBase.Texture.GetSize();
            int canvasWidth = Mathf.RoundToInt(textureSize.X);
            int canvasHeight = Mathf.RoundToInt(textureSize.Y);
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                GD.PrintErr("[EnvironmentBinder2D V14] Invalid GroundBase canvas size.");
                return;
            }

            // IMPORTANT: GroundBase is a 456x474 source image rendered at 4x in Field 01.
            // V13 accidentally wrote world-space coordinates directly into this 456x474 image and
            // also divided world-space shader coordinates by 456x474. Most trees therefore sampled
            // the clamped edge of the mask and visually did nothing. V13.1 explicitly converts
            // world positions <-> GroundBase local/source-pixel space.
            Vector2 worldOrigin = groundBase.ToGlobal(Vector2.Zero);
            Vector2 worldScale = new Vector2(
                Mathf.Max(Mathf.Abs(groundBase.GlobalScale.X), 0.0001f),
                Mathf.Max(Mathf.Abs(groundBase.GlobalScale.Y), 0.0001f));
            Vector2 worldCanvasSize = new Vector2(canvasWidth * worldScale.X, canvasHeight * worldScale.Y);

            Image image = Image.Create(canvasWidth, canvasHeight, false, Image.Format.Rgba8);
            image.Fill(Colors.Black);

            int treeCount = 0;
            int paintedCount = 0;
            foreach (Node child in treeRoot.GetChildren())
            {
                if (child is not Node2D node2D)
                {
                    continue;
                }

                treeCount++;
                TreeInfluence influence = ResolveTreeInfluence(node2D);
                if (!influence.Valid)
                {
                    continue;
                }

                if (PaintInfluence(image, influence, groundBase))
                {
                    paintedCount++;
                }
            }

            ImageTexture maskTexture = ImageTexture.CreateFromImage(image);
            ApplyTreeMaskToSprite(
                groundBase,
                maskTexture,
                worldCanvasSize,
                worldOrigin,
                TreeGroundDarkenStrength,
                TreeGroundTintStrength);

            if (TreeGroundAffectsPath)
            {
                Sprite2D dirtPath = visualGround.GetNodeOrNull<Sprite2D>("DirtPath");
                ApplyTreeMaskToSprite(
                    dirtPath,
                    maskTexture,
                    worldCanvasSize,
                    worldOrigin,
                    TreeGroundDarkenStrength * 0.86f,
                    TreeGroundTintStrength * 0.24f);
            }

            _treeGroundApplied = true;
            GD.Print(
                $"[EnvironmentBinder2D] TREE_GROUND_V14 | trees={treeCount} painted={paintedCount} | " +
                $"source={canvasWidth}x{canvasHeight} scale=({worldScale.X:0.##},{worldScale.Y:0.##}) " +
                $"world={worldCanvasSize.X:0.#}x{worldCanvasSize.Y:0.#} | affects_path={(TreeGroundAffectsPath ? "ON" : "OFF")}");
        }

        private static void ApplyTreeMaskToSprite(
            Sprite2D sprite,
            Texture2D maskTexture,
            Vector2 worldCanvasSize,
            Vector2 worldOrigin,
            float darkenStrength,
            float tintStrength)
        {
            if (sprite?.Material is not ShaderMaterial material)
            {
                return;
            }

            material.SetShaderParameter("tree_ground_influence_mask", maskTexture);
            material.SetShaderParameter("tree_ground_world_size", worldCanvasSize);
            material.SetShaderParameter("tree_ground_world_origin", worldOrigin);
            material.SetShaderParameter("tree_ground_strength", darkenStrength);
            material.SetShaderParameter("tree_ground_tint_strength", tintStrength);
        }

        private readonly struct TreeInfluence
        {
            public readonly bool Valid;
            public readonly Vector2 RootCenter;
            public readonly float RadiusX;
            public readonly float RadiusY;
            public readonly float CoreRadiusX;
            public readonly float CoreRadiusY;
            public readonly float Strength;
            public readonly float CoreStrength;
            public readonly float DownBias;

            public TreeInfluence(Vector2 rootCenter, float radiusX, float radiusY, float coreRadiusX, float coreRadiusY, float strength, float coreStrength, float downBias)
            {
                Valid = true;
                RootCenter = rootCenter;
                RadiusX = radiusX;
                RadiusY = radiusY;
                CoreRadiusX = coreRadiusX;
                CoreRadiusY = coreRadiusY;
                Strength = strength;
                CoreStrength = coreStrength;
                DownBias = downBias;
            }
        }

        private static TreeInfluence ResolveTreeInfluence(Node2D node)
        {
            CollisionShape2D collision = node.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            Vector2 rootCenter = node.GlobalPosition;
            float radiusX = 18f;
            float radiusY = 10f;
            float coreRadiusX = 12f;
            float coreRadiusY = 5.5f;
            float strength = 0.60f;
            float coreStrength = 0.95f;
            float downBias = 2.5f;

            if (collision != null)
            {
                rootCenter = node.ToGlobal(collision.Position);
                if (collision.Shape is RectangleShape2D rect)
                {
                    radiusX = Mathf.Max(16f, rect.Size.X * 0.26f);
                    radiusY = Mathf.Max(8f, rect.Size.Y * 0.28f);
                }
                else if (collision.Shape is CapsuleShape2D capsule)
                {
                    radiusX = Mathf.Max(16f, capsule.Radius * 0.9f);
                    radiusY = Mathf.Max(8f, capsule.Height * 0.18f);
                }
            }

            string scenePath = node.SceneFilePath?.ToLowerInvariant() ?? string.Empty;
            if (scenePath.Contains("apple_tree"))
            {
                radiusX *= 1.08f;
                radiusY *= 1.10f;
                coreRadiusX = radiusX * 0.54f;
                coreRadiusY = radiusY * 0.52f;
                strength = 0.62f;
                coreStrength = 0.98f;
                downBias = 3.0f;
            }
            else
            {
                coreRadiusX = radiusX * 0.52f;
                coreRadiusY = radiusY * 0.50f;
            }

            rootCenter.Y += downBias;
            return new TreeInfluence(rootCenter, radiusX, radiusY, coreRadiusX, coreRadiusY, strength, coreStrength, downBias);
        }

        private static bool PaintInfluence(Image image, TreeInfluence influence, Sprite2D groundBase)
        {
            // Convert the world-space root center to GroundBase local/source-pixel coordinates.
            Vector2 center = groundBase.ToLocal(influence.RootCenter);

            // Convert world-space radii into GroundBase source-pixel radii. This is the missing 4x
            // conversion that made V13 nearly invisible on Field 01.
            Vector2 worldScale = new Vector2(
                Mathf.Max(Mathf.Abs(groundBase.GlobalScale.X), 0.0001f),
                Mathf.Max(Mathf.Abs(groundBase.GlobalScale.Y), 0.0001f));

            float radiusX = influence.RadiusX / worldScale.X;
            float radiusY = influence.RadiusY / worldScale.Y;
            float coreRadiusX = influence.CoreRadiusX / worldScale.X;
            float coreRadiusY = influence.CoreRadiusY / worldScale.Y;

            // Slightly broaden the actual soil-response area beyond the caster contact shadow.
            radiusX *= 1.20f;
            radiusY *= 1.18f;
            coreRadiusX *= 1.10f;
            coreRadiusY *= 1.08f;

            if (center.X < -radiusX || center.X > image.GetWidth() + radiusX ||
                center.Y < -radiusY || center.Y > image.GetHeight() + radiusY)
            {
                return false;
            }

            int minX = Mathf.Clamp(Mathf.FloorToInt(center.X - radiusX - 2f), 0, image.GetWidth() - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(center.X + radiusX + 2f), 0, image.GetWidth() - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(center.Y - radiusY - 2f), 0, image.GetHeight() - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(center.Y + radiusY + 3f), 0, image.GetHeight() - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                    float body = EllipseFalloff(
                        p,
                        center + new Vector2(0f, radiusY * 0.18f),
                        radiusX,
                        radiusY) * influence.Strength;

                    float core = EllipseFalloff(
                        p,
                        center + new Vector2(0f, -0.15f),
                        coreRadiusX,
                        coreRadiusY) * influence.CoreStrength;

                    float lowerBed = EllipseFalloff(
                        p,
                        center + new Vector2(0f, radiusY * 0.34f),
                        radiusX * 0.88f,
                        radiusY * 0.74f) * influence.Strength * 0.78f;

                    float leftRoot = EllipseFalloff(
                        p,
                        center + new Vector2(-coreRadiusX * 0.67f, 0.0f),
                        coreRadiusX * 0.72f,
                        coreRadiusY * 0.72f) * influence.CoreStrength * 0.66f;

                    float rightRoot = EllipseFalloff(
                        p,
                        center + new Vector2(coreRadiusX * 0.67f, 0.0f),
                        coreRadiusX * 0.72f,
                        coreRadiusY * 0.72f) * influence.CoreStrength * 0.66f;

                    float value = Mathf.Clamp(
                        Mathf.Max(body, Mathf.Max(core, Mathf.Max(lowerBed, Mathf.Max(leftRoot, rightRoot)))),
                        0f,
                        1f);

                    if (value <= 0.001f)
                    {
                        continue;
                    }

                    float existing = image.GetPixel(x, y).R;
                    float blended = Mathf.Clamp(Mathf.Max(existing, value), 0f, 1f);
                    image.SetPixel(x, y, new Color(blended, blended, blended, 1f));
                }
            }

            return true;
        }

        private static float EllipseFalloff(Vector2 point, Vector2 center, float radiusX, float radiusY)
        {
            if (radiusX <= 0.0001f || radiusY <= 0.0001f)
            {
                return 0f;
            }

            float dx = (point.X - center.X) / radiusX;
            float dy = (point.Y - center.Y) / radiusY;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d >= 1f)
            {
                return 0f;
            }

            float t = 1f - d;
            return t * t * (3f - 2f * t);
        }
    }
}
