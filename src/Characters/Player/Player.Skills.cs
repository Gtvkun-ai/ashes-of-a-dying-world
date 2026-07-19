using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;

public partial class Player
{
    private void EnsureDefaultSkills()
    {
        CharacterConfig config = Stats?.ConfigData;
        if (config == null || (config.ID != "001" && config.Name != "Hikaru"))
        {
            return;
        }

        config.ActiveSkills ??= new Godot.Collections.Array<SkillData>();
        foreach (SkillData skill in config.ActiveSkills)
        {
            if (BuildSkillKey(skill) == "focus")
            {
                return;
            }
        }

        config.ActiveSkills.Add(CreateFocusSkill());
    }

    private static SkillData CreateFocusSkill()
    {
        return new SkillData
        {
            SkillId = "focus",
            SkillName = "Tập trung",
            Icon = GD.Load<Texture2D>("res://assets/resources/data/icon/DEX.tres"),
            Description = "Tăng 10% tốc độ di chuyển và 10% Dexterity trong 60 giây.",
            ExecutionType = SkillExecutionType.TimedBuff,
            Duration = 60f,
            Cooldown = 600f,
            MoveSpeedBonusPercent = 10f,
            DexterityBonusPercent = 10f,
            ManaCost = 0,
            StaminaCost = 0
        };
    }

    private void TryActivateSkillSlot(int slotIndex)
    {
        SkillData skill = GetSkillFromSlot(slotIndex);
        Abilities?.TryActivate(skill);
    }

    private SkillData GetSkillFromSlot(int index)
    {
        var skills = Stats?.ConfigData?.ActiveSkills;
        return skills != null && index >= 0 && index < skills.Count ? skills[index] : null;
    }

    public SkillData GetActiveTimedSkill() => Abilities?.ActiveTimedSkill;
    public float GetActiveTimedSkillRemaining() => Abilities?.ActiveTimedSkillRemaining ?? 0f;
    public float GetActiveTimedSkillDuration()
    {
        SkillData active = Abilities?.ActiveTimedSkill;
        return active == null ? 0f : Mathf.Max(0f, active.Duration);
    }

    public List<SkillSaveData> CaptureActiveSkills()
    {
        var result = new List<SkillSaveData>();
        var skills = Stats?.ConfigData?.ActiveSkills;
        if (skills == null)
        {
            return result;
        }

        foreach (SkillData skill in skills)
        {
            if (skill != null)
            {
                result.Add(CreateSkillSaveData(skill));
            }
        }
        return result;
    }

    public List<SkillCooldownSaveData> CaptureSkillCooldowns()
    {
        var result = new List<SkillCooldownSaveData>();
        if (Abilities == null)
        {
            return result;
        }

        foreach (var pair in Abilities.Cooldowns)
        {
            if (pair.Key != null && pair.Value > 0f)
            {
                result.Add(new SkillCooldownSaveData
                {
                    SkillKey = BuildSkillKey(pair.Key),
                    Remaining = pair.Value
                });
            }
        }
        return result;
    }

    public TimedSkillSaveData CaptureActiveTimedSkill()
    {
        SkillData active = Abilities?.ActiveTimedSkill;
        if (active == null)
        {
            return null;
        }

        return new TimedSkillSaveData
        {
            SkillKey = BuildSkillKey(active),
            Remaining = Abilities.ActiveTimedSkillRemaining,
            CooldownRemaining = Abilities.GetCooldownRemaining(active)
        };
    }

    public void RestoreSavedSkills(
        IReadOnlyList<SkillSaveData> activeSkills,
        IReadOnlyList<SkillCooldownSaveData> cooldowns,
        TimedSkillSaveData activeTimedSkill)
    {
        Abilities?.Clear();

        if (Stats?.ConfigData != null && activeSkills != null && activeSkills.Count > 0)
        {
            var restored = new Godot.Collections.Array<SkillData>();
            foreach (SkillSaveData data in activeSkills)
            {
                SkillData skill = CreateSkillFromSaveData(data);
                if (skill != null)
                {
                    restored.Add(skill);
                }
            }
            Stats.ConfigData.ActiveSkills = restored;
        }

        if (Abilities == null)
        {
            return;
        }

        if (cooldowns != null)
        {
            foreach (SkillCooldownSaveData cooldown in cooldowns)
            {
                SkillData skill = FindSkillByKey(cooldown?.SkillKey);
                if (skill != null && cooldown.Remaining > 0f)
                {
                    Abilities.SetCooldown(skill, cooldown.Remaining);
                }
            }
        }

        if (activeTimedSkill == null)
        {
            return;
        }

        SkillData active = FindSkillByKey(activeTimedSkill.SkillKey);
        if (active != null)
        {
            Abilities.RestoreTimedSkill(
                active,
                Mathf.Clamp(activeTimedSkill.Remaining, 0f, Mathf.Max(0f, active.Duration)),
                Mathf.Max(0f, activeTimedSkill.CooldownRemaining));
        }
    }

    private static SkillSaveData CreateSkillSaveData(SkillData skill)
    {
        return new SkillSaveData
        {
            SkillKey = BuildSkillKey(skill),
            ResourcePath = skill.ResourcePath ?? string.Empty,
            IconPath = skill.Icon?.ResourcePath ?? string.Empty,
            SkillName = skill.SkillName ?? string.Empty,
            Description = skill.Description ?? string.Empty,
            ExecutionType = (int)skill.ExecutionType,
            CombatActionPath = skill.CombatAction?.ResourcePath ?? string.Empty,
            CanUseWhileBlocking = skill.CanUseWhileBlocking,
            Duration = skill.Duration,
            MoveSpeedBonusPercent = skill.MoveSpeedBonusPercent,
            DexterityBonusPercent = skill.DexterityBonusPercent,
            HealAmount = skill.HealAmount,
            RestoreStaminaAmount = skill.RestoreStaminaAmount,
            RestoreGuardAmount = skill.RestoreGuardAmount,
            Cooldown = skill.Cooldown,
            DamageMultiplier = skill.DamageMultiplier,
            ManaCost = skill.ManaCost,
            StaminaCost = skill.StaminaCost,
            AnimationName = skill.AnimationName ?? string.Empty
        };
    }

    private static SkillData CreateSkillFromSaveData(SkillSaveData data)
    {
        if (data == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(data.ResourcePath))
        {
            SkillData loaded = GD.Load<SkillData>(data.ResourcePath);
            if (loaded != null)
            {
                return loaded;
            }
        }

        CombatActionData action = string.IsNullOrWhiteSpace(data.CombatActionPath)
            ? null
            : GD.Load<CombatActionData>(data.CombatActionPath);

        return new SkillData
        {
            SkillId = data.SkillKey,
            SkillName = data.SkillName,
            Icon = string.IsNullOrWhiteSpace(data.IconPath) ? null : GD.Load<Texture2D>(data.IconPath),
            Description = data.Description,
            ExecutionType = System.Enum.IsDefined(typeof(SkillExecutionType), data.ExecutionType)
                ? (SkillExecutionType)data.ExecutionType
                : SkillExecutionType.TimedBuff,
            CombatAction = action,
            CanUseWhileBlocking = data.CanUseWhileBlocking,
            Duration = data.Duration,
            MoveSpeedBonusPercent = data.MoveSpeedBonusPercent,
            DexterityBonusPercent = data.DexterityBonusPercent,
            HealAmount = data.HealAmount,
            RestoreStaminaAmount = data.RestoreStaminaAmount,
            RestoreGuardAmount = data.RestoreGuardAmount,
            Cooldown = data.Cooldown,
            DamageMultiplier = data.DamageMultiplier,
            ManaCost = data.ManaCost,
            StaminaCost = data.StaminaCost,
            AnimationName = data.AnimationName
        };
    }

    private SkillData FindSkillByKey(string key)
    {
        var skills = Stats?.ConfigData?.ActiveSkills;
        if (skills == null)
        {
            return null;
        }

        foreach (SkillData skill in skills)
        {
            if (BuildSkillKey(skill) == key)
            {
                return skill;
            }
        }
        return null;
    }

    private static string BuildSkillKey(SkillData skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(skill.SkillId))
        {
            return skill.SkillId.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(skill.ResourcePath))
        {
            return skill.ResourcePath;
        }

        string name = skill.SkillName?.Trim().ToLowerInvariant() ?? string.Empty;
        return name == "tập trung" || name.Contains("p trung") ? "focus" : name;
    }
}
