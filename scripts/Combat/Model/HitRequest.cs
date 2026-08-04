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
        public Vector2 HitOrigin { get; init; }
        public Vector2 AttackDirection { get; init; }
    }
}
