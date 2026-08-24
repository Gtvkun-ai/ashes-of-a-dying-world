using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Fields
{
    public static class CombatFieldSpawner
    {
        public static CombatField2D Spawn(
            CombatCharacter owner,
            CombatActionData action,
            CombatFieldSpecData spec,
            float damageMultiplier = 1f)
        {
            if (owner == null || action == null || spec == null || owner.GetTree() == null)
            {
                return null;
            }

            Node parent = owner.GetTree().CurrentScene ?? owner.GetTree().Root;
            if (parent == null)
            {
                return null;
            }

            var field = new CombatField2D { Name = $"CombatField_{spec.FieldId}" };
            field.Initialize(owner, action, spec, damageMultiplier);
            parent.AddChild(field);
            field.GlobalPosition = owner.GlobalPosition + spec.GroundOffset;
            return field;
        }
    }
}
