using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Entities.Player;
using System.Collections.Generic;

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
if (skill?.SkillName == "Tập trung")
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
SkillName = "Tập trung",
Icon = GD.Load<Texture2D>("res://assets/resources/data/icon/DEX.tres"),
Description = "Tăng 10% tốc chạy và 10% DEX trong 1 phút.",
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
GD.Print($"[Player] Skill {skill.SkillName} is on cooldown: {cooldownRemaining:F1}s");
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
GD.Print($"[Player] Activated skill: {skill.SkillName}");

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
}
