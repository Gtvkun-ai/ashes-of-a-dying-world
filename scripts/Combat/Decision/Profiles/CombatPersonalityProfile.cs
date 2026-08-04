using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Profiles
{
    /// <summary>
    /// Bias mềm tạo sắc thái hành vi. Đây không phải state và không được vượt hard gate mechanics.
    /// </summary>
    [GlobalClass]
    public partial class CombatPersonalityProfile : Resource
    {
        [Export(PropertyHint.Range, "0,1,0.01")] public float Protectiveness { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float SelfPreservation { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Patience { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Discipline { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Confidence { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float StressSensitivity { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Adaptability { get; set; } = 0.5f;
    }
}
