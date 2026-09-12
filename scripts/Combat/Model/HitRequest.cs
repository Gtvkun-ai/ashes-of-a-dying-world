using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Model
{
    public sealed class HitRequest
    {
        public CombatCharacter Attacker { get; init; }
        public CombatCharacter Target { get; init; }
        public CombatActionData Action { get; init; }
        public HitProfileData Profile { get; init; }
        public float DamageMultiplier { get; init; } = 1f;
        // Scale riêng cho displacement/reaction. SpringJump cần charge sâu mạnh hơn nhưng không nhất thiết tăng damage.
        public float ImpactMultiplier { get; init; } = 1f;
        public Vector2 HitOrigin { get; init; }
        public Vector2 AttackDirection { get; init; }
    }
}
