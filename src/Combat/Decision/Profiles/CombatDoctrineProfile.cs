using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Profiles
{
    /// <summary>
    /// Học thuyết chiến đấu có thể chỉnh bằng Resource thay vì sinh thêm một lớp AI mới.
    /// </summary>
    [GlobalClass]
    public partial class CombatDoctrineProfile : Resource
    {
        [Export(PropertyHint.Range, "0,1,0.01")] public float Aggression { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RiskTolerance { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RangeDiscipline { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ResourceConservation { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float LeaderProtection { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MobilityPreference { get; set; } = 0.5f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RetreatReadiness { get; set; } = 0.5f;
    }
}
