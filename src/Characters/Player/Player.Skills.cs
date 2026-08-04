using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Core.Skills;

public partial class Player
{
    private readonly PlayerSkillCollection _skillCollection = new();

    /// <summary>
    /// Khởi tạo kho kỹ năng runtime từ CharacterConfig hiện tại.
    /// CharacterConfig chỉ đóng vai trò nguồn định nghĩa; loadout không còn sửa trực tiếp Resource.
    /// </summary>
    private void InitializeSkillCollection()
    {
        EnsureDefaultSkills();
        _skillCollection.Initialize(Stats?.ConfigData);
    }

    /// <summary>
    /// Giữ kỹ năng mặc định Tập trung cho Hikaru.
    /// Phần này vẫn thêm định nghĩa vào config, nhưng trạng thái cấp/slot nằm trong PlayerSkillCollection.
    /// </summary>
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
            Category = SkillCategory.Active,
            Element = SkillElement.None,
            MaxLevel = 1,
            DefaultUnlocked = true,
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
        return _skillCollection.GetEquippedSkill(index);
    }

    // API dành cho UI. UI đọc state qua đây thay vì tự sửa CharacterConfig.ActiveSkills.
    public IReadOnlyList<SkillData> GetKnownSkills() => _skillCollection.GetDefinitions();
    public PlayerSkillState GetSkillState(SkillData skill) => _skillCollection.GetState(skill);
    public int GetUnspentSkillPoints() => _skillCollection.UnspentSkillPoints;
    public bool OwnsSkill(SkillData skill) => _skillCollection.Contains(skill);

    public bool TryEquipSkill(SkillData skill, int slotIndex)
    {
        return _skillCollection.TryEquip(skill, slotIndex);
    }

    public bool TryUnequipSkill(int slotIndex)
    {
        return _skillCollection.TryUnequip(slotIndex);
    }

    public bool TryUpgradeSkill(SkillData skill)
    {
        return _skillCollection.TryUpgrade(skill);
    }

    public void GrantSkillPoints(int amount)
    {
        _skillCollection.GrantSkillPoints(amount);
    }

    public SkillData GetEquippedSkill(int slotIndex)
    {
        return _skillCollection.GetEquippedSkill(slotIndex);
    }

    public SkillData GetActiveTimedSkill() => Abilities?.ActiveTimedSkill;
    public float GetActiveTimedSkillRemaining() => Abilities?.ActiveTimedSkillRemaining ?? 0f;

    public float GetActiveTimedSkillDuration()
    {
        SkillData active = Abilities?.ActiveTimedSkill;
        return active == null ? 0f : Mathf.Max(0f, active.Duration);
    }

    /// <summary>
    /// Save mới chỉ lưu trạng thái thay đổi của người chơi, không chép lại tên/icon/damage của Resource.
    /// </summary>
    public List<SkillStateSaveData> CaptureSkillStates()
    {
        var result = new List<SkillStateSaveData>();
        foreach (PlayerSkillState state in _skillCollection.CaptureStates())
        {
            result.Add(new SkillStateSaveData
            {
                SkillId = state.SkillId,
                Level = state.Level,
                IsUnlocked = state.IsUnlocked,
                EquippedSlot = state.EquippedSlot
            });
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

    /// <summary>
    /// Khôi phục hệ thống kỹ năng mới và đồng thời hỗ trợ save cũ.
    ///
    /// - skillStates có dữ liệu: dùng mô hình mới, chỉ restore level/unlock/slot.
    /// - skillStates rỗng nhưng legacyActiveSkills có dữ liệu: migrate save cũ một lần.
    /// </summary>
    public void RestoreSavedSkills(
        IReadOnlyList<SkillSaveData> legacyActiveSkills,
        IReadOnlyList<SkillStateSaveData> skillStates,
        int unspentSkillPoints,
        IReadOnlyList<SkillCooldownSaveData> cooldowns,
        TimedSkillSaveData activeTimedSkill)
    {
        Abilities?.Clear();

        bool hasNewState = skillStates != null && skillStates.Count > 0;
        if (!hasNewState && Stats?.ConfigData != null && legacyActiveSkills != null && legacyActiveSkills.Count > 0)
        {
            // Migration save v2: dựng lại danh sách định nghĩa từ dữ liệu cũ.
            var restoredDefinitions = new Godot.Collections.Array<SkillData>();
            foreach (SkillSaveData data in legacyActiveSkills)
            {
                SkillData skill = CreateSkillFromLegacySaveData(data);
                if (skill != null)
                {
                    restoredDefinitions.Add(skill);
                }
            }

            if (restoredDefinitions.Count > 0)
            {
                Stats.ConfigData.ActiveSkills = restoredDefinitions;
            }
        }

        // SaveManager có thể vừa đổi CharacterConfig sau khi Player đã Ready,
        // vì vậy luôn khởi tạo lại collection tại đây.
        InitializeSkillCollection();

        if (hasNewState)
        {
            var runtimeStates = new List<PlayerSkillState>();
            foreach (SkillStateSaveData data in skillStates)
            {
                if (data == null)
                {
                    continue;
                }

                runtimeStates.Add(new PlayerSkillState
                {
                    SkillId = data.SkillId,
                    Level = data.Level,
                    IsUnlocked = data.IsUnlocked,
                    EquippedSlot = data.EquippedSlot
                });
            }

            _skillCollection.RestoreStates(runtimeStates, unspentSkillPoints);
        }
        else
        {
            // Save cũ dùng thứ tự ActiveSkills làm loadout; Initialize đã giữ đúng hành vi đó.
            _skillCollection.SetUnspentSkillPoints(Math.Max(0, unspentSkillPoints));
        }

        RestoreSkillRuntime(cooldowns, activeTimedSkill);
    }

    private void RestoreSkillRuntime(
        IReadOnlyList<SkillCooldownSaveData> cooldowns,
        TimedSkillSaveData activeTimedSkill)
    {
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

    /// <summary>
    /// Chỉ dùng để đọc save cũ. Save mới không còn sao chép toàn bộ SkillData.
    /// </summary>
    private static SkillData CreateSkillFromLegacySaveData(SkillSaveData data)
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
            Category = Enum.IsDefined(typeof(SkillCategory), data.Category)
                ? (SkillCategory)data.Category
                : SkillCategory.Active,
            Element = Enum.IsDefined(typeof(SkillElement), data.Element)
                ? (SkillElement)data.Element
                : SkillElement.None,
            MaxLevel = Math.Max(1, data.MaxLevel),
            DefaultUnlocked = true,
            ExecutionType = Enum.IsDefined(typeof(SkillExecutionType), data.ExecutionType)
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
        SkillData fromCollection = _skillCollection.GetDefinition(key);
        if (fromCollection != null)
        {
            return fromCollection;
        }

        CharacterConfig config = Stats?.ConfigData;
        SkillData found = FindSkillInCollection(config?.ActiveSkills, key);
        return found ?? FindSkillInCollection(config?.ComboSequence, key);
    }

    private static SkillData FindSkillInCollection(
        Godot.Collections.Array<SkillData> skills,
        string key)
    {
        if (skills == null)
        {
            return null;
        }

        foreach (SkillData skill in skills)
        {
            if (BuildSkillKey(skill) == PlayerSkillCollection.NormalizeSkillId(key))
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
            return PlayerSkillCollection.NormalizeSkillId(skill.SkillId);
        }

        if (!string.IsNullOrWhiteSpace(skill.ResourcePath))
        {
            return PlayerSkillCollection.NormalizeSkillId(skill.ResourcePath);
        }

        string name = PlayerSkillCollection.NormalizeSkillId(skill.SkillName);
        return name == "tập trung" || name.Contains("p trung") ? "focus" : name;
    }
}
