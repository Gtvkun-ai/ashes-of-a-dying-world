using Godot;

namespace AshesofaDyingWorld.Core.Data
{
    [GlobalClass]
    public partial class SkillData : Resource
    {
        [Export] public string SkillName { get; set; }
        [Export] public Texture2D Icon {get; set;}
        [Export(PropertyHint.MultilineText)] public string Description { get; set; }
        
        [ExportGroup("Combat Specs")]
        [Export] public float Cooldown { get; set; } = 5.0f;
        [Export] public float DamageMultiplier { get; set; } = 1.0f;
        [Export] public int ManaCost { get; set; } = 10;
        [Export] public int StaminaCost { get; set; } = 20;

        //Animation name trong AnimationPlayer
        [Export] public string AnimationName { get; set; }
    }
}