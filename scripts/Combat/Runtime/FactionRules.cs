using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Luật phe được tách thành hai câu hỏi khác nhau:
    /// - IsHostile: AI có được xem actor kia là mục tiêu chiến đấu hay không.
    /// - CanDamage: một hit vật lý thật có được gây sát thương hay không.
    ///
    /// Player và Companion vẫn là đồng minh đối với AI, nhưng có friendly fire với nhau.
    /// Nhờ vậy Hyou không bao giờ chủ động target Hikaru, trong khi một viên đạn bay nhầm
    /// hoặc một nhát kiếm quét trúng đồng đội vẫn có hậu quả thật.
    /// </summary>
    public static class FactionRules
    {
        public static bool AreAllies(CombatFaction a, CombatFaction b)
        {
            if (a == CombatFaction.Neutral || b == CombatFaction.Neutral)
            {
                return false;
            }

            bool aFriendly = IsPartyFaction(a);
            bool bFriendly = IsPartyFaction(b);
            return (aFriendly && bFriendly) || (a == CombatFaction.Enemy && b == CombatFaction.Enemy);
        }

        /// <summary>
        /// Dùng cho perception/target selection. Friendly fire tuyệt đối không được biến
        /// đồng đội thành hostile target, nếu không AI sẽ "thông minh" tới mức quay sang săn nhau.
        /// </summary>
        public static bool IsHostile(CombatFaction observer, CombatFaction candidate)
        {
            if (observer == CombatFaction.Neutral || candidate == CombatFaction.Neutral)
            {
                return false;
            }

            if (IsPartyFaction(observer))
            {
                return candidate == CombatFaction.Enemy;
            }

            return observer == CombatFaction.Enemy && IsPartyFaction(candidate);
        }

        /// <summary>
        /// Luật damage thật. Player <-> Companion có friendly fire; Enemy <-> Enemy hiện vẫn tắt.
        /// Tách khỏi IsHostile để AI không chủ động đánh đồng đội nhưng physics vẫn trung thực.
        /// </summary>
        public static bool CanDamage(CombatFaction attacker, CombatFaction target)
        {
            if (attacker == CombatFaction.Neutral || target == CombatFaction.Neutral)
            {
                return false;
            }

            if (IsHostile(attacker, target))
            {
                return true;
            }

            // Mọi thành viên party đều có thể gây friendly fire cho nhau. Self-hit được chặn
            // ở CombatResolver bằng identity của actor, vì chỉ nhìn enum faction thì không phân biệt
            // hai companion khác nhau trong tương lai.
            return IsPartyFaction(attacker) && IsPartyFaction(target);
        }

        private static bool IsPartyFaction(CombatFaction faction)
        {
            return faction == CombatFaction.Player || faction == CombatFaction.Companion;
        }
    }
}
