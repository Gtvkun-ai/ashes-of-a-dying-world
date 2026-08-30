using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Skills;

namespace AshesofaDyingWorld.UI.HUD.Skills
{
    /// <summary>
    /// Dữ liệu đã được định dạng cho UI.
    /// CharacterDetailUI chỉ lo dựng control, không tự lặp lại công thức format ở nhiều nơi.
    /// </summary>
    public sealed class SkillViewModel
    {
        public SkillData Definition { get; }
        public PlayerSkillState State { get; }

        public SkillViewModel(SkillData definition, PlayerSkillState state)
        {
            Definition = definition;
            State = state ?? new PlayerSkillState
            {
                SkillId = PlayerSkillCollection.NormalizeSkillId(definition),
                Level = 1,
                IsUnlocked = definition?.DefaultUnlocked ?? true,
                EquippedSlot = -1
            };
        }

        public string Name => string.IsNullOrWhiteSpace(Definition?.SkillName)
            ? "Kỹ năng"
            : Definition.SkillName;

        public Texture2D Icon => SkillIconResolver.Resolve(Definition);
        public bool IsUnlocked => State.IsUnlocked;
        public bool IsEquipped => State.EquippedSlot >= 0;
        public int EquippedSlot => State.EquippedSlot;
        public int Level => State.Level;
        public int MaxLevel => Mathf.Max(1, Definition?.MaxLevel ?? 1);

        public bool CanEquip => Definition != null
            && Definition.Category == SkillCategory.Active
            && IsUnlocked;

        public string LevelText => MaxLevel > 1 ? $"Lv.{Level}/{MaxLevel}" : string.Empty;

        public string CategoryText => Definition?.Category switch
        {
            SkillCategory.Passive => "Bị động",
            SkillCategory.Innate => "Nội tại",
            _ => "Chủ động"
        };

        public string ElementText => Definition?.Element switch
        {
            SkillElement.Physical => "Vật lý",
            SkillElement.Ice => "Băng",
            SkillElement.Fire => "Lửa",
            SkillElement.Lightning => "Sét",
            SkillElement.Wind => "Gió",
            SkillElement.Earth => "Đất",
            SkillElement.Light => "Ánh sáng",
            SkillElement.Dark => "Bóng tối",
            SkillElement.Arcane => "Bí thuật",
            _ => string.Empty
        };

        public string RoleText
        {
            get
            {
                if (Definition == null)
                {
                    return string.Empty;
                }

                if (Definition.ExecutionType == SkillExecutionType.Heal)
                {
                    return "Hồi phục";
                }

                if (Definition.ExecutionType == SkillExecutionType.RestoreResources)
                {
                    return "Hồi tài nguyên";
                }

                if (Definition.ExecutionType == SkillExecutionType.TimedBuff)
                {
                    return "Cường hóa";
                }

                return Definition.CombatAction?.HitProfile?.DamageType == DamageType.Magic
                    ? "Phép"
                    : "Tấn công";
            }
        }

        public string RangeText
        {
            get
            {
                if (Definition?.CombatAction == null)
                {
                    return Definition?.ExecutionType == SkillExecutionType.TimedBuff
                        ? "Bản thân"
                        : string.Empty;
                }

                return Definition.CombatAction.DeliveryMode switch
                {
                    CombatDeliveryMode.Projectile => "Tầm xa",
                    CombatDeliveryMode.MeleeHitbox => "Cận chiến",
                    CombatDeliveryMode.SelfEffect => "Bản thân",
                    _ => string.Empty
                };
            }
        }

        public string SubtitleText
        {
            get
            {
                var parts = new List<string> { CategoryText };
                if (!string.IsNullOrWhiteSpace(ElementText))
                {
                    parts.Add(ElementText);
                }
                else if (!string.IsNullOrWhiteSpace(RoleText))
                {
                    parts.Add(RoleText);
                }

                return string.Join(" · ", parts);
            }
        }

        public string QuickStatsText => $"{CostText}      {CooldownText}";

        public string DamageText
        {
            get
            {
                HitProfileData hit = Definition?.CombatAction?.HitProfile;
                if (hit != null)
                {
                    int baseDamage = Mathf.RoundToInt(hit.BaseDamage);
                    // CombatResolver hiện dùng trực tiếp AttackPowerScale, nên UI phải hiển thị đúng cùng nguồn dữ liệu.
                    int scalePercent = Mathf.RoundToInt(hit.AttackPowerScale * 100f);
                    if (baseDamage > 0 || scalePercent > 0)
                    {
                        return $"{baseDamage} + {scalePercent}% Công";
                    }
                }

                if (Definition != null && Definition.HealAmount > 0f)
                {
                    return $"Hồi {Mathf.RoundToInt(Definition.HealAmount)} HP";
                }

                string bonus = BonusSummary;
                return string.IsNullOrWhiteSpace(bonus) ? "Không gây sát thương" : bonus;
            }
        }

        public string CooldownText => Definition == null || Definition.Cooldown <= 0f
            ? "Tức thì"
            : FormatSeconds(Definition.Cooldown);

        public string CostText
        {
            get
            {
                var parts = new List<string>();
                if (Definition != null && Definition.ManaCost > 0)
                {
                    parts.Add($"{Definition.ManaCost} MP");
                }
                if (Definition != null && Definition.StaminaCost > 0)
                {
                    parts.Add($"{Definition.StaminaCost} STA");
                }

                return parts.Count == 0 ? "Miễn phí" : string.Join(" · ", parts);
            }
        }

        public string CastTimeText
        {
            get
            {
                float castTime = Definition?.CombatAction?.StartupSeconds ?? 0f;
                return castTime <= 0.05f ? "Tức thì" : FormatSeconds(castTime);
            }
        }

        public string BonusSummary
        {
            get
            {
                var parts = new List<string>();
                if (Definition == null)
                {
                    return string.Empty;
                }

                if (Definition.MoveSpeedBonusPercent > 0f)
                {
                    parts.Add($"+{Mathf.RoundToInt(Definition.MoveSpeedBonusPercent)}% tốc độ");
                }
                if (Definition.DexterityBonusPercent > 0f)
                {
                    parts.Add($"+{Mathf.RoundToInt(Definition.DexterityBonusPercent)}% DEX");
                }
                if (Definition.AutoEvadeChancePercent > 0f)
                {
                    parts.Add(Definition.AutoEvadeUseRelativeMastery
                        ? $"Né bản năng động (mốc {Mathf.RoundToInt(Definition.AutoEvadeChancePercent)}%)"
                        : $"{Mathf.RoundToInt(Definition.AutoEvadeChancePercent)}% né bản năng");
                }
                if (Definition.RestoreStaminaAmount > 0f)
                {
                    parts.Add($"Hồi {Mathf.RoundToInt(Definition.RestoreStaminaAmount)} STA");
                }
                if (Definition.RestoreGuardAmount > 0f)
                {
                    parts.Add($"Hồi {Mathf.RoundToInt(Definition.RestoreGuardAmount)} Guard");
                }

                return string.Join(", ", parts);
            }
        }

        public IReadOnlyList<string> GetBadges()
        {
            var badges = new List<string> { CategoryText };
            AddDistinct(badges, ElementText);
            AddDistinct(badges, RoleText);
            AddDistinct(badges, RangeText);
            return badges;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static string FormatSeconds(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;
            return Mathf.IsEqualApprox(rounded, Mathf.Round(rounded))
                ? $"{Mathf.RoundToInt(rounded)} giây"
                : $"{rounded:0.0} giây";
        }
    }
}
