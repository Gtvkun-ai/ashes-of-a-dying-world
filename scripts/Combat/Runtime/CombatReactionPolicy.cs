using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Luật duy nhất quyết định một reaction có được phép cắt action đang chạy hay không.
    /// Tách riêng để Flinch/Shove không vô tình phá combo, còn Stagger+ vẫn là hard interrupt.
    /// </summary>
    public static class CombatReactionPolicy
    {
        public static bool ShouldInterruptActiveAction(
            ImpactReactionType reaction,
            bool actionRunning,
            bool actionUninterruptible)
        {
            if (!actionRunning)
            {
                return false;
            }

            // Stagger / Knockdown / Launch luôn thắng action, kể cả action có tag Uninterruptible.
            if ((int)reaction >= (int)ImpactReactionType.Stagger)
            {
                return true;
            }

            // Knockback là ngưỡng giữa: action thường bị cắt, action Uninterruptible được giữ.
            if (reaction == ImpactReactionType.Knockback)
            {
                return !actionUninterruptible;
            }

            // Absorb / Flinch / Shove chỉ là phản ứng mềm: vẫn nhận VFX, hitstop, displacement,
            // nhưng không được hủy attack đang chạy.
            return false;
        }
    }
}
