using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Static clearance bằng context probes 16 hướng.
    /// P0 vẫn giữ ray fallback vì nó không đòi thay scene/navmesh, nhưng probe được cập nhật thích nghi:
    /// ngoài vùng chật chỉ refresh nửa số ray mỗi tick; gần vật cản hoặc lúc kẹt thì refresh đủ 16.
    /// </summary>
    internal sealed class CombatStaticClearance
    {
        public const int DirectionCount = 16;

        private readonly CombatCharacter _self;
        private readonly uint _obstacleMask;
        private readonly float _probeDistance;
        private readonly CombatMovementMetrics _metrics;
        private readonly RayCast2D[] _rays = new RayCast2D[DirectionCount];
        private readonly float[] _dangerCache = new float[DirectionCount];

        private Node2D _rig;
        private int _probeParity;
        private bool _wasNearObstacle;

        public CombatStaticClearance(
            CombatCharacter self,
            uint obstacleMask,
            float probeDistance,
            CombatMovementMetrics metrics)
        {
            _self = self;
            _obstacleMask = obstacleMask;
            _probeDistance = Mathf.Max(8f, probeDistance);
            _metrics = metrics;
            BuildSensorRig();
        }

        public void Refresh(Vector2 selfPosition, bool forceDense)
        {
            bool dense = forceDense || _wasNearObstacle;
            float strongest = 0f;
            int parity = _probeParity;

            for (int slot = 0; slot < DirectionCount; slot++)
            {
                if (!dense && (slot & 1) != parity)
                {
                    strongest = Mathf.Max(strongest, _dangerCache[slot]);
                    continue;
                }

                RayCast2D ray = _rays[slot];
                float danger = 0f;
                if (ray != null && GodotObject.IsInstanceValid(ray) && ray.IsInsideTree())
                {
                    ray.ForceRaycastUpdate();
                    _metrics?.RecordProbe();
                    if (ray.IsColliding())
                    {
                        float hitDistance = selfPosition.DistanceTo(ray.GetCollisionPoint());
                        danger = 1f - Mathf.Clamp(hitDistance / _probeDistance, 0f, 1f);
                    }
                }

                _dangerCache[slot] = danger;
                strongest = Mathf.Max(strongest, danger);
            }

            _probeParity ^= 1;
            _wasNearObstacle = strongest > 0.08f;
        }

        public float GetDanger(int slot)
        {
            int center = Wrap(slot);
            int left1 = Wrap(slot - 1);
            int right1 = Wrap(slot + 1);
            int left2 = Wrap(slot - 2);
            int right2 = Wrap(slot + 2);

            // Lan danger sang ray kề để AI chừa clearance cho thân, không liếm sát góc chỉ vì ray tâm vừa lọt.
            float danger = _dangerCache[center];
            danger = Mathf.Max(danger, _dangerCache[left1] * 0.72f);
            danger = Mathf.Max(danger, _dangerCache[right1] * 0.72f);
            danger = Mathf.Max(danger, _dangerCache[left2] * 0.34f);
            danger = Mathf.Max(danger, _dangerCache[right2] * 0.34f);
            return Mathf.Clamp(danger, 0f, 1f);
        }

        public bool IsNearObstacle => _wasNearObstacle;

        public void Dispose()
        {
            if (_rig != null && GodotObject.IsInstanceValid(_rig))
            {
                _rig.QueueFree();
            }
            _rig = null;
        }

        private void BuildSensorRig()
        {
            if (_self == null || !_self.IsInsideTree())
            {
                return;
            }

            _rig = new Node2D { Name = "CombatStaticClearanceRuntime" };
            for (int slot = 0; slot < DirectionCount; slot++)
            {
                float angle = Mathf.Tau * slot / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                var ray = new RayCast2D
                {
                    Name = $"ClearanceRay{slot:00}",
                    Enabled = true,
                    TargetPosition = direction * _probeDistance,
                    CollisionMask = _obstacleMask,
                    CollideWithAreas = false,
                    CollideWithBodies = true,
                    ExcludeParent = false
                };
                _rig.AddChild(ray);
                ray.AddException(_self);
                _rays[slot] = ray;
            }

            _self.CallDeferred("add_child", _rig);
        }

        private static int Wrap(int slot)
        {
            int wrapped = slot % DirectionCount;
            return wrapped < 0 ? wrapped + DirectionCount : wrapped;
        }
    }
}
