using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Hitbox runtime duy nhất của combat.
    ///
    /// Area2D luôn tồn tại và luôn theo dõi hurtbox, nhưng chỉ gây hit khi _active = true.
    /// Không bật/tắt Monitoring hoặc Monitorable trong callback vật lý, vì Godot cấm đổi
    /// trạng thái truy vấn khi đang flush AreaEntered/AreaExited. Cách cũ chính là nguồn
    /// tạo ra hàng chục lỗi NativeCalls.godot_icall_1_14 mỗi lần nhân vật đánh trúng nhau.
    /// </summary>
    public partial class CombatHitbox : Node2D
    {
        private CombatCharacter _combatOwner;
        private Area2D _area;
        private CollisionShape2D _collisionShape;
        private RectangleShape2D _shape;
        private CombatActionData _action;
        private HitProfileData _profile;
        private readonly HashSet<ulong> _hitTargets = new();
        private bool _active;

        public bool IsActive => _active;

        public void Initialize(CombatCharacter combatOwner)
        {
            _combatOwner = combatOwner;
            Name = "CombatHitboxRuntime";
            EnsureNodes();
            DisableHitbox();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_active)
            {
                ScanOverlaps();
            }
        }

        public void EnableHitbox(CombatActionData action, Vector2 facing)
        {
            if (action?.HitProfile == null || _combatOwner == null)
            {
                DisableHitbox();
                return;
            }

            EnsureNodes();
            _action = action;
            _profile = action.HitProfile;
            _hitTargets.Clear();

            Vector2 safeFacing = facing == Vector2.Zero ? Vector2.Down : facing.Normalized();
            float angle = safeFacing.Angle() - Mathf.Pi * 0.5f;
            Rotation = angle;
            Position = safeFacing * _profile.Reach + _profile.LocalOffset.Rotated(angle);
            _shape.Size = new Vector2(
                Mathf.Max(1f, _profile.HitboxSize.X),
                Mathf.Max(1f, _profile.HitboxSize.Y));

            _active = true;
            SetPhysicsProcess(true);
            _area.ForceUpdateTransform();
        }

        public void DisableHitbox()
        {
            _active = false;
            _action = null;
            _profile = null;
            _hitTargets.Clear();
            SetPhysicsProcess(false);
        }

        private void EnsureNodes()
        {
            if (_area != null)
            {
                return;
            }

            _area = new Area2D
            {
                Name = "Area",
                CollisionLayer = 8,
                CollisionMask = 16,

                // Hitbox chỉ cần chủ động quét Hurtbox. Nó không cần cho Area khác phát hiện.
                // Giữ hai cờ cố định suốt vòng đời để không đụng giới hạn physics query của Godot.
                Monitoring = true,
                Monitorable = false
            };
            AddChild(_area);

            _shape = new RectangleShape2D { Size = new Vector2(18f, 28f) };
            _collisionShape = new CollisionShape2D
            {
                Name = "Shape",
                Shape = _shape,
                Disabled = false
            };
            _area.AddChild(_collisionShape);
        }

        private void ScanOverlaps()
        {
            if (!_active || _area == null)
            {
                return;
            }

            foreach (Area2D area in _area.GetOverlappingAreas())
            {
                TryHit(area);
            }
        }

        private void TryHit(Area2D area)
        {
            if (!_active || area == null || _combatOwner == null || _profile == null)
            {
                return;
            }

            CombatCharacter target = FindCombatCharacter(area);
            if (target == null || target == _combatOwner)
            {
                return;
            }

            ulong targetId = target.GetInstanceId();
            if (!_hitTargets.Add(targetId))
            {
                return;
            }

            _combatOwner.TryResolveHit(target, _action, _profile);
        }

        private static CombatCharacter FindCombatCharacter(Node node)
        {
            Node cursor = node;
            for (int depth = 0; cursor != null && depth < 8; depth++)
            {
                if (cursor is CombatCharacter combatCharacter)
                {
                    return combatCharacter;
                }

                cursor = cursor.GetParent();
            }

            return null;
        }
    }
}
