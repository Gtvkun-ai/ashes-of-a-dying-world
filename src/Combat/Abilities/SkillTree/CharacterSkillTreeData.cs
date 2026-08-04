using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Toàn bộ cây kỹ năng của một nhân vật.
    /// CharacterConfig tham chiếu Resource này, còn panel chỉ đọc dữ liệu và render.
    /// </summary>
    [GlobalClass]
    public partial class CharacterSkillTreeData : Resource
    {
        [Export] public string CharacterId { get; set; } = "";
        [Export] public Array<SkillTreeBranchData> Branches { get; set; } = new();
    }
}
