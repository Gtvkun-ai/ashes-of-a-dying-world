using System;

namespace AshesofaDyingWorld.Combat.Decision.Model
{
    /// <summary>
    /// Ý định chiến thuật trung gian. Decision Core chỉ chọn intent;
    /// mechanics hiện có vẫn là nơi thực thi chuyển động, action và damage.
    /// </summary>
    public enum CombatIntentType
    {
        None = 0,
        HoldRange = 1,
        Approach = 2,
        Backpedal = 3,
        StrafeLeft = 4,
        StrafeRight = 5,
        OrbitClockwise = 6,
        OrbitCounterClockwise = 7,
        Guard = 8,
        CastPrimary = 9,
        CastSecondary = 10,
        CastDefensive = 11,
        RecoverResources = 12,
        ProtectLeader = 13,
        Reposition = 14,
        PanicEvade = 15
    }

    [Flags]
    public enum CombatInterruptMask
    {
        None = 0,
        Dead = 1 << 0,
        Hitstun = 1 << 1,
        GuardBreak = 1 << 2,
        TargetInvalid = 1 << 3,
        EmergencyEvade = 1 << 4
    }

    [Flags]
    public enum TacticalActionTag
    {
        None = 0,
        Damage = 1 << 0,
        Control = 1 << 1,
        Defensive = 1 << 2,
        Escape = 1 << 3,
        Protect = 1 << 4,
        Projectile = 1 << 5,
        Area = 1 << 6,
        LowCommitment = 1 << 7,
        HighCommitment = 1 << 8,
        RequiresLos = 1 << 9,
        RequiresRangeBand = 1 << 10,
        ManaHeavy = 1 << 11,
        StaminaHeavy = 1 << 12,
        Recover = 1 << 13,
        Mobility = 1 << 14
    }

    public enum CombatRoleId
    {
        Unassigned = 0,
        Frontline = 1,
        BacklineController = 2,
        Protector = 3,
        Flanker = 4,
        Skirmisher = 5,
        Support = 6
    }
}
