using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    public static class FactionRules
    {
        public static bool AreAllies(CombatFaction a, CombatFaction b)
        {
            if (a == CombatFaction.Neutral || b == CombatFaction.Neutral)
            {
                return false;
            }

            bool aFriendly = a == CombatFaction.Player || a == CombatFaction.Companion;
            bool bFriendly = b == CombatFaction.Player || b == CombatFaction.Companion;
            return (aFriendly && bFriendly) || (a == CombatFaction.Enemy && b == CombatFaction.Enemy);
        }

        public static bool CanDamage(CombatFaction attacker, CombatFaction target)
        {
            if (attacker == CombatFaction.Neutral || target == CombatFaction.Neutral)
            {
                return false;
            }

            return !AreAllies(attacker, target);
        }
    }
}
