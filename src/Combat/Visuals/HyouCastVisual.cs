using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Presentation riêng của Hyou. Visual nghe ActionRunner thay vì để AI tự gọi,
    /// nên legacy brain hay Decision Core đều dùng cùng một nhịp cast.
    /// </summary>
    public partial class HyouCastVisual : Node2D
    {
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public NodePath OwnerWeaponPath { get; set; } = new NodePath("../WeaponSprite");

        [ExportGroup("Magic Sheets")]
        [Export] public string BackIceSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou bh ice .png";
        [Export] public string BackIceBoltSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou bh ice bolt.png";
        [Export] public string IceBehindSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou ice bh.png";
        [Export] public string IceUpSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou ice up.png";
        [Export] public string UpIceSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou up ice.png";
        [Export] public string UpIceBoltSheetPath { get; set; } = "res://assets/sprites/char/Hyou/11x4 scale 0.1 hyou ice bolt/scaled_0_1/x10 hyou up ice bolt.png";

        [ExportGroup("Grid")]
        [Export] public int Columns { get; set; } = 11;
        [Export] public int Rows { get; set; } = 4;
        [Export] public int FrameWidth { get; set; } = 48;
        [Export] public int FrameHeight { get; set; } = 64;
        [Export] public int UsedFrames { get; set; } = 8;
        [Export] public float AnimationSpeed { get; set; } = 5f;

        [ExportGroup("Direction Rows")]
        [Export] public bool ForceUpAnimation { get; set; } = false;
        [Export] public int DownRow { get; set; } = 0;
        [Export] public int RightRow { get; set; } = 1;
        [Export] public int LeftRow { get; set; } = 2;
        [Export] public int UpRow { get; set; } = 3;

        private AnimatedSprite2D[] _layers = System.Array.Empty<AnimatedSprite2D>();
        private AnimatedSprite2D _ownerWeapon;
        private CombatCharacter _character;
        private float _remaining;
        private bool _weaponWasVisible;
        private bool _bound;

        public override void _Ready()
        {
            _ownerWeapon = GetNodeOrNull<AnimatedSprite2D>(OwnerWeaponPath);
            _layers = new[]
            {
                EnsureLayer("BackIce", BackIceSheetPath, -6, UpRow),
                EnsureLayer("BackIceBolt", BackIceBoltSheetPath, -4, UpRow),
                EnsureLayer("IceBehind", IceBehindSheetPath, -2, UpRow),
                EnsureLayer("IceUp", IceUpSheetPath, 2, DownRow),
                EnsureLayer("UpIce", UpIceSheetPath, 4, DownRow),
                EnsureLayer("UpIceBolt", UpIceBoltSheetPath, 6, DownRow),
            };

            Visible = false;
            SetProcess(true);
            CallDeferred(nameof(BindActionRunner));
        }

        public override void _ExitTree()
        {
            UnbindActionRunner();
        }

        public override void _Process(double delta)
        {
            CombatActionData currentAction = _character?.Actions?.CurrentAction;
            if (currentAction != null && currentAction.DeliveryMode == CombatDeliveryMode.Projectile)
            {
                if (!Visible)
                {
                    PlayCast(_character.Actions.ActionFacing);
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
                StopCast();
            }
        }

        public void PlayCast(Vector2 facing)
        {
            string animation = $"ice_bolt_{(ForceUpAnimation ? "up" : ResolveDirection(facing))}";
            foreach (AnimatedSprite2D layer in _layers)
            {
                PlayLayer(layer, animation);
            }

            if (_ownerWeapon != null)
            {
                _weaponWasVisible = _ownerWeapon.Visible;
                _ownerWeapon.Visible = false;
            }

            Visible = true;
            _remaining = Mathf.Max(0.05f, UsedFrames / Mathf.Max(0.1f, AnimationSpeed));
            SetProcess(true);
        }

        public void StopCast()
        {
            Visible = false;
            foreach (AnimatedSprite2D layer in _layers)
            {
                StopLayer(layer);
            }

            if (_ownerWeapon != null)
            {
                _ownerWeapon.Visible = _weaponWasVisible;
            }
        }

        private void BindActionRunner()
        {
            if (_bound || !IsInsideTree())
            {
                return;
            }

            string path = CharacterPath.ToString();
            _character = string.IsNullOrWhiteSpace(path)
                ? GetParentOrNull<CombatCharacter>()
                : GetNodeOrNull<CombatCharacter>(CharacterPath);
            if (_character?.Actions == null)
            {
                // Child _Ready chạy trước parent _Ready trong Godot. Hoãn thêm một nhịp,
                // thay vì giả vờ Actions đã tồn tại rồi nhận null như một nghi thức truyền thống.
                CallDeferred(nameof(BindActionRunner));
                return;
            }

            _character.Actions.ActionStarted += OnActionStarted;
            _character.Actions.ActionFinished += OnActionFinished;
            _bound = true;
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

        private void OnActionStarted(CombatActionData action, Vector2 facing)
        {
            if (action == null || action.DeliveryMode != CombatDeliveryMode.Projectile)
            {
                return;
            }

            PlayCast(facing);
        }

        private void OnActionFinished(CombatActionData action, bool completed)
        {
            if (action != null && action.DeliveryMode == CombatDeliveryMode.Projectile)
            {
                StopCast();
            }
        }

        private AnimatedSprite2D EnsureLayer(string layerName, string sheetPath, int zIndex, int castRow)
        {
            var layer = GetNodeOrNull<AnimatedSprite2D>(layerName);
            if (layer == null)
            {
                layer = new AnimatedSprite2D { Name = layerName };
                AddChild(layer);
            }

            layer.Centered = true;
            layer.ZIndex = zIndex;
            layer.Visible = true;
            layer.SpriteFrames = BuildFrames(sheetPath, castRow);
            return layer;
        }

        private SpriteFrames BuildFrames(string sheetPath, int castRow)
        {
            var frames = new SpriteFrames();
            Texture2D sheet = GD.Load<Texture2D>(sheetPath);
            AddDirection(frames, sheet, "down", castRow);
            AddDirection(frames, sheet, "right", castRow);
            AddDirection(frames, sheet, "left", castRow);
            AddDirection(frames, sheet, "up", castRow);
            return frames;
        }

        private void AddDirection(SpriteFrames frames, Texture2D sheet, string direction, int row)
        {
            string animation = $"ice_bolt_{direction}";
            frames.AddAnimation(animation);
            frames.SetAnimationLoop(animation, false);
            frames.SetAnimationSpeed(animation, AnimationSpeed);

            if (sheet == null)
            {
                return;
            }

            int safeRow = Mathf.Clamp(row, 0, Mathf.Max(0, Rows - 1));
            int frameCount = Mathf.Clamp(UsedFrames, 1, Columns);
            for (int column = 0; column < frameCount; column++)
            {
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
