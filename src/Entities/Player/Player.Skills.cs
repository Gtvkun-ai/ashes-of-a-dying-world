using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Entities.Player;

public partial class Player
{
private void EnsureDefaultSkills()
{
var config = _stats?.ConfigData;
if (config == null)
{
return;
}

if (config.ID != "001" && config.Name != "Hikaru")
{
return;
}

config.ActiveSkills ??= new Godot.Collections.Array<SkillData>();

foreach (var skill in config.ActiveSkills)
{
if (skill?.SkillName == "Táº­p trung")
{
return;
}
}

config.ActiveSkills.Add(CreateFocusSkill());
}

private SkillData CreateFocusSkill()
{
return new SkillData
{
SkillName = "Táº­p trung",
Icon = GD.Load<Texture2D>("res://assets/resources/data/icon/DEX.tres"),
Description = "TÄƒng 10% tá»‘c cháº¡y vÃ  10% DEX trong 1 phÃºt.",
Duration = 60.0f,
Cooldown = 600.0f,
MoveSpeedBonusPercent = 10.0f,
DexterityBonusPercent = 10.0f,
ManaCost = 0,
StaminaCost = 0,
AnimationName = ""
};
}

private void UpdateSkillTimers(float delta)
{
if (_activeTimedSkill != null)
{
_activeTimedSkillRemaining -= delta;
if (_activeTimedSkillRemaining <= 0f)
{
EndActiveTimedSkill();
}
}

if (_skillCooldowns.Count == 0)
{
return;
}

var skills = new List<SkillData>(_skillCooldowns.Keys);
foreach (var skill in skills)
{
float remaining = _skillCooldowns[skill] - delta;
if (remaining <= 0f)
{
_skillCooldowns.Remove(skill);
}
else
{
_skillCooldowns[skill] = remaining;
}
}
}

private void TryActivateSkillSlot(int slotIndex)
{
var skill = GetSkillFromSlot(slotIndex);
if (skill == null)
{
return;
}

if (_skillCooldowns.TryGetValue(skill, out float cooldownRemaining) && cooldownRemaining > 0f)
{
return;
}

ActivateTimedSkill(skill);
}

private SkillData GetSkillFromSlot(int slotIndex)
{
var skills = _stats?.ConfigData?.ActiveSkills;
if (skills == null || slotIndex < 0 || slotIndex >= skills.Count)
{
return null;
}

return skills[slotIndex];
}

private void ActivateTimedSkill(SkillData skill)
{
if (_stats == null || skill == null)
{
return;
}

EndActiveTimedSkill();

_activeTimedSkill = skill;
_activeTimedSkillRemaining = Mathf.Max(0f, skill.Duration);
_activeMoveSpeedMultiplier = 1.0f + (skill.MoveSpeedBonusPercent / 100.0f);
_activeDexterityBonus = ComputePercentAttributeBonus(AttributeType.Dexterity, skill.DexterityBonusPercent);

if (_activeDexterityBonus != 0)
{
_stats.SetTemporaryAttributeBonus(AttributeType.Dexterity, _activeDexterityBonus);
}

_skillCooldowns[skill] = Mathf.Max(0f, skill.Cooldown);

if (_activeTimedSkillRemaining <= 0f)
{
EndActiveTimedSkill();
}
}

private int ComputePercentAttributeBonus(AttributeType attributeType, float bonusPercent)
{
if (_stats == null || bonusPercent <= 0f)
{
return 0;
}

int currentValue = _stats.GetAttributeValue(attributeType);
if (currentValue <= 0)
{
return 0;
}

return Mathf.Max(1, Mathf.RoundToInt(currentValue * bonusPercent / 100.0f));
}

private void EndActiveTimedSkill()
{
if (_stats != null && _activeDexterityBonus != 0)
{
_stats.SetTemporaryAttributeBonus(AttributeType.Dexterity, 0);
}

_activeTimedSkill = null;
_activeTimedSkillRemaining = 0f;
_activeMoveSpeedMultiplier = 1f;
_activeDexterityBonus = 0;
}

private float GetMoveSpeedMultiplier()
{
return _activeTimedSkill != null ? _activeMoveSpeedMultiplier : 1f;
}

public PlayerStats GetStatsNode()
{
return _stats;
}

public InventoryManager GetInventoryManager()
{
return _inventory;
}

public EquipmentManager GetEquipmentManager()
{
return _equipMgr;
}

public SkillData GetActiveTimedSkill()
{
return _activeTimedSkill;
}

public float GetActiveTimedSkillRemaining()
{
return Mathf.Max(0f, _activeTimedSkillRemaining);
}

public float GetActiveTimedSkillDuration()
{
return _activeTimedSkill != null ? Mathf.Max(0f, _activeTimedSkill.Duration) : 0f;
}

public List<SkillSaveData> CaptureActiveSkills()
{
var result = new List<SkillSaveData>();
var skills = _stats?.ConfigData?.ActiveSkills;
if (skills == null)
{
return result;
}

foreach (var skill in skills)
{
if (skill == null)
{
continue;
}

result.Add(CreateSkillSaveData(skill));
}

return result;
}

public List<SkillCooldownSaveData> CaptureSkillCooldowns()
{
var result = new List<SkillCooldownSaveData>();
foreach (var pair in _skillCooldowns)
{
if (pair.Key == null || pair.Value <= 0f)
{
continue;
}

result.Add(new SkillCooldownSaveData
{
SkillKey = BuildSkillKey(pair.Key),
Remaining = pair.Value
});
}

return result;
}

public TimedSkillSaveData CaptureActiveTimedSkill()
{
if (_activeTimedSkill == null)
{
return null;
}

float cooldownRemaining = 0f;
if (_skillCooldowns.TryGetValue(_activeTimedSkill, out float storedCooldown))
{
cooldownRemaining = storedCooldown;
}

return new TimedSkillSaveData
{
SkillKey = BuildSkillKey(_activeTimedSkill),
Remaining = Mathf.Max(0f, _activeTimedSkillRemaining),
CooldownRemaining = Mathf.Max(0f, cooldownRemaining)
};
}

public void RestoreSavedSkills(
IReadOnlyList<SkillSaveData> activeSkills,
IReadOnlyList<SkillCooldownSaveData> cooldowns,
TimedSkillSaveData activeTimedSkill)
{
EndActiveTimedSkill();
_skillCooldowns.Clear();

if (_stats?.ConfigData != null && activeSkills != null && activeSkills.Count > 0)
{
var restoredSkills = new Godot.Collections.Array<SkillData>();
foreach (var skillData in activeSkills)
{
SkillData skill = CreateSkillFromSaveData(skillData);
if (skill != null)
{
restoredSkills.Add(skill);
}
}

_stats.ConfigData.ActiveSkills = restoredSkills;
}

if (cooldowns != null)
{
foreach (var cooldown in cooldowns)
{
SkillData skill = FindSkillByKey(cooldown?.SkillKey);
if (skill == null || cooldown.Remaining <= 0f)
{
continue;
}

_skillCooldowns[skill] = cooldown.Remaining;
}
}

if (activeTimedSkill == null)
{
return;
}

SkillData activeSkill = FindSkillByKey(activeTimedSkill.SkillKey);
if (activeSkill == null)
{
return;
}

ActivateTimedSkill(activeSkill);
_activeTimedSkillRemaining = Mathf.Clamp(
activeTimedSkill.Remaining,
0f,
Mathf.Max(0f, activeSkill.Duration));
_skillCooldowns[activeSkill] = Mathf.Max(0f, activeTimedSkill.CooldownRemaining);
}

private SkillSaveData CreateSkillSaveData(SkillData skill)
{
return new SkillSaveData
{
SkillKey = BuildSkillKey(skill),
ResourcePath = skill.ResourcePath ?? string.Empty,
IconPath = skill.Icon?.ResourcePath ?? string.Empty,
SkillName = skill.SkillName ?? string.Empty,
Description = skill.Description ?? string.Empty,
Duration = skill.Duration,
MoveSpeedBonusPercent = skill.MoveSpeedBonusPercent,
DexterityBonusPercent = skill.DexterityBonusPercent,
Cooldown = skill.Cooldown,
DamageMultiplier = skill.DamageMultiplier,
ManaCost = skill.ManaCost,
StaminaCost = skill.StaminaCost,
AnimationName = skill.AnimationName ?? string.Empty
};
}

private SkillData CreateSkillFromSaveData(SkillSaveData saveData)
{
if (saveData == null)
{
return null;
}

if (!string.IsNullOrEmpty(saveData.ResourcePath))
{
SkillData resourceSkill = GD.Load<SkillData>(saveData.ResourcePath);
if (resourceSkill != null)
{
return resourceSkill;
}
}

return new SkillData
{
SkillName = saveData.SkillName,
Icon = !string.IsNullOrEmpty(saveData.IconPath) ? GD.Load<Texture2D>(saveData.IconPath) : null,
Description = saveData.Description,
Duration = saveData.Duration,
MoveSpeedBonusPercent = saveData.MoveSpeedBonusPercent,
DexterityBonusPercent = saveData.DexterityBonusPercent,
Cooldown = saveData.Cooldown,
DamageMultiplier = saveData.DamageMultiplier,
ManaCost = saveData.ManaCost,
StaminaCost = saveData.StaminaCost,
AnimationName = saveData.AnimationName
};
}

private SkillData FindSkillByKey(string skillKey)
{
if (string.IsNullOrEmpty(skillKey))
{
return null;
}

var skills = _stats?.ConfigData?.ActiveSkills;
if (skills == null)
{
return null;
}

foreach (var skill in skills)
{
if (skill != null && BuildSkillKey(skill) == skillKey)
{
return skill;
}
}

return null;
}

private string BuildSkillKey(SkillData skill)
{
if (skill == null)
{
return string.Empty;
}

if (!string.IsNullOrEmpty(skill.ResourcePath))
{
return $"path:{skill.ResourcePath}";
}

return $"name:{skill.SkillName}";
}
}
