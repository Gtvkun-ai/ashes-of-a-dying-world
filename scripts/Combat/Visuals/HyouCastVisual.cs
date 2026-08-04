using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Presentation riêng cho Ice Bolt của Hyou.
    ///
    /// Sáu sheet phép dùng chung timeline 11x4 với body cast. Trong tám frame cast:
    /// - cột 0-1: chuẩn bị, VFX cố ý trong suốt;
    /// - cột 2-5: vòng phép và lõi băng hiện ra;
    /// - cột 6-7: kết thúc/release, VFX lại trong suốt.
    ///
    /// Vì vậy tuyệt đối không cắt sheet thành "bốn frame từ cột 0". Làm thế chỉ
    /// phát hai frame rỗng rồi hai frame đầu của phép, một cách khá sáng tạo để
    /// biến vòng phép thành thứ người chơi phải tưởng tượng.
    /// </summary>
    public partial class HyouCastVisual : Node2D
    {
        private const string RuntimeBuild = "v6-projectile-resource-soft-pursuit";
        [ExportGroup("Binding")]
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public NodePath OwnerWeaponPath { get; set; } = new NodePath("../WeaponSprite");
        [Export] public string CastActionId { get; set; } = "hyou_ice_bolt";

        [ExportGroup("Magic Sheets")]
        [Export] public string BackIceSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/back_ice.png";
        [Export] public string BackIceBoltSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/back_ice_bolt.png";
        [Export] public string IceBehindSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/ice_behind.png";
        [Export] public string IceUpSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/ice_up.png";
        [Export] public string UpIceSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/up_ice.png";
        [Export] public string UpIceBoltSheetPath { get; set; } = "res://assets/graphics/characters/hyou/vfx/ice_bolt/up_ice_bolt.png";

        [ExportGroup("Sheet Grid")]
        [Export] public int Columns { get; set; } = 11;
        [Export] public int Rows { get; set; } = 4;
        [Export] public int FrameWidth { get; set; } = 48;
        [Export] public int FrameHeight { get; set; } = 64;
        [Export] public int TimelineStartColumn { get; set; } = 0;
        [Export] public int TimelineFrames { get; set; } = 8;

        [ExportGroup("Cast Timing")]
        [Export(PropertyHint.Range, "0.1,10.0,0.05")]
        public float CastDurationSeconds { get; set; } = 2f;

        [ExportGroup("Direction Rows")]
        [Export] public int DownRow { get; set; } = 0;
        [Export] public int RightRow { get; set; } = 1;
        [Export] public int LeftRow { get; set; } = 2;
        [Export] public int UpRow { get; set; } = 3;

        [ExportGroup("Diagnostics")]
        [Export] public bool DebugLogging { get; set; } = true;

        private AnimatedSprite2D[] _layers = System.Array.Empty<AnimatedSprite2D>();
        private AnimatedSprite2D _ownerWeapon;
        private CombatCharacter _character;
        private CombatActionData _playingAction;
        private float _remaining;
        private bool _weaponWasVisible;
        private bool _bound;

        public override void _Ready()
        {
            _ownerWeapon = GetNodeOrNull<AnimatedSprite2D>(OwnerWeaponPath);
            _layers = new[]
            {
                // Ba lớp nằm sau body. Chúng chỉ có pixel ở row up (row 3).
                EnsureLayer("BackIce", BackIceSheetPath, -6),
                EnsureLayer("BackIceBolt", BackIceBoltSheetPath, -4),
                EnsureLayer("IceBehind", IceBehindSheetPath, -2),

                // Ba lớp nằm trước body. IceUp là lõi băng, UpIceBolt là vòng phép.
                EnsureLayer("IceUp", IceUpSheetPath, 2),
                EnsureLayer("UpIce", UpIceSheetPath, 4),
                EnsureLayer("UpIceBolt", UpIceBoltSheetPath, 6),
            };

            Visible = false;
            SetProcess(true);
            TryBindActionRunner();

            if (DebugLogging)
            {
                GD.Print($"[HyouCastVisual] READY build={RuntimeBuild} timeline={TimelineFrames} frames duration={CastDurationSeconds:0.00}s node={GetPath()}");
            }
        }

        public override void _ExitTree()
        {
            UnbindActionRunner();
        }

        public override void _Process(double delta)
        {
            if (!_bound)
            {
                // Child _Ready chạy trước parent _Ready. Parent tạo ActionRunner sau đó,
                // nên bind lại ở process đầu tiên thay vì đệ quy CallDeferred vô hạn.
                TryBindActionRunner();
            }

            CombatActionData currentAction = _character?.Actions?.CurrentAction;
            if (MatchesCastAction(currentAction))
            {
                // Fallback nếu signal ActionStarted bị lỡ vì thứ tự khởi tạo scene.
                if (!Visible || _playingAction != currentAction)
                {
                    PlayCast(currentAction, ResolveCharacterFacing());
                }
                return;
            }

            if (!Visible)
            {
                return;
            }

            _remaining -= Mathf.Max(0f, (float)delta);
            if (_remaining <= 0f)
            {
                StopCast("duration_elapsed");
            }
        }

        public void PlayCast(CombatActionData action, Vector2 facing)
        {
            if (!MatchesCastAction(action))
            {
                return;
            }

            string direction = ResolveDirection(facing);
            string animation = $"ice_bolt_{direction}";

            Visible = true;
            foreach (AnimatedSprite2D layer in _layers)
            {
                PlayLayer(layer, animation);
            }

            if (_ownerWeapon != null)
            {
                _weaponWasVisible = _ownerWeapon.Visible;
                _ownerWeapon.Visible = false;
            }

            _playingAction = action;
            _remaining = Mathf.Max(0.1f, CastDurationSeconds);

            if (DebugLogging)
            {
                GD.Print($"[HyouCastVisual] CAST START build={RuntimeBuild} action={action.ActionId} dir={direction} animation={animation} duration={_remaining:0.00}s");
            }
        }

        public void StopCast(string reason = "action_finished")
        {
            if (!Visible && _playingAction == null)
            {
                return;
            }

            Visible = false;
            _playingAction = null;
            _remaining = 0f;

            foreach (AnimatedSprite2D layer in _layers)
            {
                StopLayer(layer);
            }

            if (_ownerWeapon != null)
            {
                _ownerWeapon.Visible = _weaponWasVisible;
            }

            if (DebugLogging)
            {
                GD.Print($"[HyouCastVisual] CAST STOP reason={reason}");
            }
        }

        private bool TryBindActionRunner()
        {
            if (_bound || !IsInsideTree())
            {
                return _bound;
            }

            string path = CharacterPath.ToString();
            _character = string.IsNullOrWhiteSpace(path)
                ? GetParentOrNull<CombatCharacter>()
                : GetNodeOrNull<CombatCharacter>(CharacterPath);

            if (_character?.Actions == null)
            {
                return false;
            }

            _character.Actions.ActionStarted += OnActionStarted;
            _character.Actions.ActionFinished += OnActionFinished;
            _bound = true;

            if (DebugLogging)
            {
                GD.Print($"[HyouCastVisual] BOUND character={_character.CombatantId}");
            }
            return true;
        }

        private void UnbindActionRunner()
        {
            if (!_bound || _character?.Actions == null)
            {
                return;
            }

            _character.Actions.ActionStarted -= OnActionStarted;
            _character.Actions.ActionFinished -= OnActionFinished;
            _bound = false;
        }

        private void OnActionStarted(CombatActionData action, Vector2 aimFacing)
        {
            if (!MatchesCastAction(action))
            {
                return;
            }

            // Body animation dùng FacingCardinal của character, không dùng vector aim chéo.
            // Visual phải bám cùng hướng body để các row của sáu sheet chồng khít nhau.
            PlayCast(action, ResolveCharacterFacing(aimFacing));
        }

        private void OnActionFinished(CombatActionData action, bool completed)
        {
            if (MatchesCastAction(action))
            {
                StopCast(completed ? "action_completed" : "action_cancelled");
            }
        }

        private bool MatchesCastAction(CombatActionData action)
        {
            if (action == null || action.DeliveryMode != CombatDeliveryMode.Projectile)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(CastActionId)
                || string.Equals(action.ActionId, CastActionId, System.StringComparison.OrdinalIgnoreCase);
        }

        private AnimatedSprite2D EnsureLayer(string layerName, string sheetPath, int zIndex)
        {
            var layer = GetNodeOrNull<AnimatedSprite2D>(layerName);
            if (layer == null)
            {
                layer = new AnimatedSprite2D { Name = layerName };
                AddChild(layer);
            }

            layer.Centered = true;
            layer.Position = Vector2.Zero;
            layer.ZIndex = zIndex;
            layer.Visible = true;
            layer.SpriteFrames = BuildFrames(sheetPath);
            return layer;
        }

        private SpriteFrames BuildFrames(string sheetPath)
        {
            var frames = new SpriteFrames();
            Texture2D sheet = GD.Load<Texture2D>(sheetPath);
            if (sheet == null)
            {
                GD.PushError($"[HyouCastVisual] Không load được magic sheet: {sheetPath}");
            }
            else
            {
                int expectedWidth = Columns * FrameWidth;
                int expectedHeight = Rows * FrameHeight;
                if (sheet.GetWidth() < expectedWidth || sheet.GetHeight() < expectedHeight)
                {
                    GD.PushError($"[HyouCastVisual] Sheet sai kích thước: {sheetPath} actual={sheet.GetWidth()}x{sheet.GetHeight()} expected>={expectedWidth}x{expectedHeight}");
                }
            }

            AddDirection(frames, sheet, "down", DownRow);
            AddDirection(frames, sheet, "right", RightRow);
            AddDirection(frames, sheet, "left", LeftRow);
            AddDirection(frames, sheet, "up", UpRow);
            return frames;
        }

        private void AddDirection(SpriteFrames frames, Texture2D sheet, string direction, int row)
        {
            string animation = $"ice_bolt_{direction}";
            frames.AddAnimation(animation);
            frames.SetAnimationLoop(animation, false);

            int frameCount = Mathf.Clamp(TimelineFrames, 1, Columns);
            // Frame 0 hiện ngay tại t=0. Muốn frame 7 xuất hiện đúng t=2 giây thì
            // tốc độ phải là (8 - 1) / 2 = 3.5 FPS, trùng với body 5 FPS * 0.7.
            float animationSpeed = frameCount <= 1
                ? 1f
                : (frameCount - 1) / Mathf.Max(0.1f, CastDurationSeconds);
            frames.SetAnimationSpeed(animation, animationSpeed);

            if (sheet == null)
            {
                return;
            }

            int safeRow = Mathf.Clamp(row, 0, Mathf.Max(0, Rows - 1));
            int maxStart = Mathf.Max(0, Columns - frameCount);
            int startColumn = Mathf.Clamp(TimelineStartColumn, 0, maxStart);
            for (int frame = 0; frame < frameCount; frame++)
            {
                int column = startColumn + frame;
                var atlas = new AtlasTexture
                {
                    Atlas = sheet,
                    Region = new Rect2(column * FrameWidth, safeRow * FrameHeight, FrameWidth, FrameHeight)
                };
                frames.AddFrame(animation, atlas);
            }
        }

        private static void PlayLayer(AnimatedSprite2D layer, string animation)
        {
            if (layer?.SpriteFrames == null || !layer.SpriteFrames.HasAnimation(animation))
            {
                return;
            }

            layer.Visible = true;
            layer.Animation = animation;
            layer.Frame = 0;
            layer.Play();
        }

        private static void StopLayer(AnimatedSprite2D layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.Stop();
            layer.Frame = 0;
        }

        private Vector2 ResolveCharacterFacing(Vector2 fallback = default)
        {
            if (_character != null)
            {
                return _character.FacingCardinal switch
                {
                    "right" => Vector2.Right,
                    "left" => Vector2.Left,
                    "up" => Vector2.Up,
                    _ => Vector2.Down,
                };
            }

            return fallback.LengthSquared() > 0.001f ? fallback : Vector2.Down;
        }

        private static string ResolveDirection(Vector2 facing)
        {
            if (facing.LengthSquared() <= 0.001f)
            {
                return "down";
            }

            Vector2 normalized = facing.Normalized();
            if (Mathf.Abs(normalized.X) > Mathf.Abs(normalized.Y))
            {
                return normalized.X >= 0f ? "right" : "left";
            }

            return normalized.Y >= 0f ? "down" : "up";
        }
    }
}
