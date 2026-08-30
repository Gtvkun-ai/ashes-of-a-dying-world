using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Executor chung cho ability: cooldown, resource cost, buff, heal, hồi tài nguyên và combat action.
    /// Player chỉ chọn slot; luật chạy skill không còn nằm rải trong Player partial.
    /// </summary>
    public sealed class CombatAbilityRunner
    {
        private readonly CombatCharacter _owner;
        private readonly Dictionary<SkillData, float> _cooldowns = new();

        private SkillData _activeTimedSkill;
        private float _activeTimedSkillRemaining;
        private float _moveSpeedMultiplier = 1f;
        private string _activeModifierSource = string.Empty;

        public IReadOnlyDictionary<SkillData, float> Cooldowns => _cooldowns;
        public SkillData ActiveTimedSkill => _activeTimedSkill;
        public float ActiveTimedSkillRemaining => Mathf.Max(0f, _activeTimedSkillRemaining);
        public float MoveSpeedMultiplier => _moveSpeedMultiplier;

        public CombatAbilityRunner(CombatCharacter owner)
        {
            _owner = owner;
        }

        public void Update(float delta)
        {
            float dt = Mathf.Max(0f, delta);
            if (_activeTimedSkill != null)
            {
                _activeTimedSkillRemaining -= dt;
                if (_activeTimedSkillRemaining <= 0f)
                {
                    EndActiveTimedSkill();
                }
            }

            if (_cooldowns.Count == 0)
            {
                return;
            }

            var skills = new List<SkillData>(_cooldowns.Keys);
            foreach (SkillData skill in skills)
            {
                float remaining = _cooldowns[skill] - dt;
                if (remaining <= 0f)
                {
                    _cooldowns.Remove(skill);
                }
                else
                {
                    _cooldowns[skill] = remaining;
                }
            }
        }

        public bool TryActivate(SkillData skill)
        {
            return TryActivate(skill, Vector2.Zero);
        }

        /// <summary>
        /// Overload cho AI/ranged skill truyền hướng ngắm chính xác. Player cũ vẫn dùng API một tham số.
        /// </summary>
        public bool TryActivate(SkillData skill, Vector2 aimDirection)
        {
            return TryActivate(skill, aimDirection, null);
        }

        public bool TryActivate(SkillData skill, Vector2 aimDirection, CombatCharacter aimTarget)
        {
            if (skill == null || _owner == null || !_owner.IsAlive || _owner.Stats == null)
            {
                return false;
            }

            if (GetCooldownRemaining(skill) > 0f)
            {
                return false;
            }

            if (_owner.IsBlocking && !skill.CanUseWhileBlocking)
            {
                return false;
            }

            float actionStamina = skill.ExecutionType == SkillExecutionType.CombatAction
                ? Mathf.Max(0f, skill.CombatAction?.StaminaCost ?? 0f)
                : 0f;
            if (_owner.Stats.CurrentMP + 0.001f < Mathf.Max(0, skill.ManaCost)
                || _owner.Stats.CurrentStamina + 0.001f < Mathf.Max(0, skill.StaminaCost) + actionStamina)
            {
                return false;
            }

            bool executed = skill.ExecutionType switch
            {
                SkillExecutionType.CombatAction => ExecuteCombatAction(skill, aimDirection, aimTarget),
                SkillExecutionType.Heal => ExecuteHeal(skill),
                SkillExecutionType.RestoreResources => ExecuteResourceRestore(skill),
                _ => ExecuteTimedBuff(skill)
            };

            if (!executed)
            {
                return false;
            }

            ConsumeSkillCosts(skill);
            _cooldowns[skill] = Mathf.Max(0f, skill.Cooldown);
            CombatFeedbackService.GetOrCreate(_owner.GetTree())?
                .PlaySkillActivated(_owner, skill);
            return true;
        }

        public float GetCooldownRemaining(SkillData skill)
        {
            return skill != null && _cooldowns.TryGetValue(skill, out float remaining)
                ? Mathf.Max(0f, remaining)
                : 0f;
        }

        public void SetCooldown(SkillData skill, float remaining)
        {
            if (skill == null)
            {
                return;
            }

            if (remaining <= 0f)
            {
                _cooldowns.Remove(skill);
            }
            else
            {
                _cooldowns[skill] = remaining;
            }
        }

        public void RestoreTimedSkill(SkillData skill, float remaining, float cooldownRemaining)
        {
            EndActiveTimedSkill();
            if (skill == null || skill.ExecutionType != SkillExecutionType.TimedBuff || remaining <= 0f)
            {
                return;
            }

            ApplyTimedBuff(skill, remaining);
            SetCooldown(skill, cooldownRemaining);
        }

        public void CancelActiveEffects()
        {
            EndActiveTimedSkill();
        }

        public void Clear()
        {
            CancelActiveEffects();
            _cooldowns.Clear();
        }

        private bool ExecuteCombatAction(SkillData skill, Vector2 aimDirection, CombatCharacter aimTarget)
        {
            if (skill.CombatAction == null || _owner.Actions == null)
            {
                return false;
            }

            if (_owner.IsBlocking && skill.CanUseWhileBlocking)
            {
                _owner.ReleaseBlock();
            }

            return _owner.Actions.TryStartAbilityAction(
                skill.CombatAction,
                aimDirection,
                aimTarget,
                Mathf.Max(0f, skill.DamageMultiplier));
        }

        private bool ExecuteHeal(SkillData skill)
        {
            float amount = Mathf.Max(0f, skill.HealAmount);
            if (amount <= 0f || _owner.Stats.CurrentHP >= _owner.Stats.MaxHP)
            {
                return false;
            }

            _owner.Stats.ChangeHP(amount);
            return true;
        }

        private bool ExecuteResourceRestore(SkillData skill)
        {
            bool canRestoreStamina = skill.RestoreStaminaAmount > 0f
                && _owner.Stats.CurrentStamina < _owner.Stats.MaxStamina;
            bool canRestoreGuard = skill.RestoreGuardAmount > 0f
                && _owner.Stats.CurrentGuard < _owner.Stats.MaxGuard;
            if (!canRestoreStamina && !canRestoreGuard)
            {
                return false;
            }

            if (canRestoreStamina)
            {
                _owner.Stats.ChangeStamina(skill.RestoreStaminaAmount);
            }
            if (canRestoreGuard)
            {
                _owner.Stats.ChangeGuard(skill.RestoreGuardAmount);
            }
            return true;
        }

        private bool ExecuteTimedBuff(SkillData skill)
        {
            ApplyTimedBuff(skill, Mathf.Max(0f, skill.Duration));
            ApplyTimedActivationResources(skill);
            return true;
        }

        /// <summary>
        /// Timed buffs may include a small immediate resource swing in addition to their duration effect.
        /// This keeps tempo skills such as Hikaru Focus responsive without inventing a separate execution type.
        /// </summary>
        private void ApplyTimedActivationResources(SkillData skill)
        {
            if (skill == null || _owner?.Stats == null)
            {
                return;
            }

            if (skill.RestoreStaminaAmount > 0f)
            {
                _owner.Stats.ChangeStamina(skill.RestoreStaminaAmount);
            }

            if (skill.RestoreGuardAmount > 0f)
            {
                _owner.Stats.ChangeGuard(skill.RestoreGuardAmount);
            }
        }

        private void ApplyTimedBuff(SkillData skill, float duration)
        {
            EndActiveTimedSkill();
            _activeTimedSkill = skill;
            _activeTimedSkillRemaining = duration;
            _moveSpeedMultiplier = Mathf.Max(0.1f, 1f + skill.MoveSpeedBonusPercent / 100f);
            _activeModifierSource = $"ability:{BuildSkillKey(skill)}";

            int dexterityBonus = ComputePercentAttributeBonus(AttributeType.Dexterity, skill.DexterityBonusPercent);
            if (dexterityBonus != 0)
            {
                _owner.Stats.SetTemporaryAttributeBonus(_activeModifierSource, AttributeType.Dexterity, dexterityBonus);
            }

            if (_activeTimedSkillRemaining <= 0f)
            {
                EndActiveTimedSkill();
            }
        }

        private void EndActiveTimedSkill()
        {
            if (_owner?.Stats != null && !string.IsNullOrWhiteSpace(_activeModifierSource))
            {
                _owner.Stats.ClearTemporaryAttributeBonuses(_activeModifierSource);
            }

            _activeTimedSkill = null;
            _activeTimedSkillRemaining = 0f;
            _moveSpeedMultiplier = 1f;
            _activeModifierSource = string.Empty;
        }

        private int ComputePercentAttributeBonus(AttributeType type, float percent)
        {
            if (_owner?.Stats == null || percent <= 0f)
            {
                return 0;
            }

            int value = _owner.Stats.GetAttributeValue(type);
            return value <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(value * percent / 100f));
        }

        private void ConsumeSkillCosts(SkillData skill)
        {
            if (skill.ManaCost > 0)
            {
                _owner.Stats.ConsumeMana(skill.ManaCost);
            }

            if (skill.StaminaCost > 0)
            {
                // Đã pre-check tổng cost trước khi action chạy, nên consume ở đây không thể fail
                // trừ khi một hệ thống ngoài chen vào giữa cùng physics tick.
                _owner.Stats.ConsumeStamina(skill.StaminaCost);
            }
        }

        private static string BuildSkillKey(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.SkillId))
            {
                return skill.SkillId.Trim().ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(skill?.ResourcePath))
            {
                return skill.ResourcePath;
            }

            return skill == null ? "unknown" : skill.GetInstanceId().ToString();
        }
    }
}
