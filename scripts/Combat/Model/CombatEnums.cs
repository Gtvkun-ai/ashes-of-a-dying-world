using System;

namespace AshesofaDyingWorld.Combat.Model
{
    public enum CombatFaction
    {
        Neutral = 0,
        Player = 1,
        Companion = 2,
        Enemy = 3
    }

    public enum CombatStateId
    {
        Locomotion = 0,
        Blocking = 1,
        AttackStartup = 2,
        AttackActive = 3,
        AttackRecovery = 4,
        BlockStun = 5,
        Hitstun = 6,
        Stagger = 7,
        GuardBreak = 8,
        Dead = 9
    }

    public enum DamageType
    {
        Physical = 0,
        Slash = 1,
        Blunt = 2,
        Pierce = 3,
        Magic = 4,
        True = 5,
        Ice = 6
    }

    /// <summary>
    /// Nguồn sức mạnh dùng để scale một hit. Auto suy ra từ DamageType.
    /// </summary>
    public enum PowerScalingType
    {
        Auto = 0,
        Physical = 1,
        Magic = 2,
        Highest = 3,
        None = 4
    }

    public enum HitRejectionReason
    {
        None = 0,
        InvalidRequest = 1,
        TargetDead = 2,
        FriendlyFire = 3,
        DuplicateTarget = 4,
        Evaded = 5
    }

    [Flags]
    public enum CombatActionTag
    {
        None = 0,
        Light = 1 << 0,
        Heavy = 1 << 1,
        Melee = 1 << 2,
        Ranged = 1 << 3,
        GuardCounter = 1 << 4,
        Uninterruptible = 1 << 5
    }
}
