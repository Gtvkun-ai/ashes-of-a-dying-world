using Godot;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Decision.Profiles
{
    /// <summary>
    /// Khả năng và khoảng cách ưa thích của class.
    /// Không chứa logic if/else riêng cho từng nhân vật.
    /// </summary>
    [GlobalClass]
    public partial class CombatClassProfile : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ClassId { get; set; } = "unassigned";

        [ExportGroup("Granted Skills")]
        [Export] public Godot.Collections.Array<SkillData> GrantedSkills { get; set; } = new();

        [ExportGroup("Range Doctrine")]
        [Export] public float PreferredMinRange { get; set; } = 36f;
        [Export] public float PreferredMaxRange { get; set; } = 48f;

        [ExportGroup("Resources")]
        [Export] public bool UsesMana { get; set; } = false;
        [Export] public bool UsesStamina { get; set; } = true;

        public SkillData GetPrimarySkill()
        {
            return GrantedSkills != null && GrantedSkills.Count > 0
                ? GrantedSkills[0]
                : null;
        }
    }
}
