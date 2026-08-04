using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Một nhánh phát triển, ví dụ Kiếm thuật hoặc Băng thuật.
    /// Dữ liệu nhánh nằm trong Resource để designer có thể sửa bằng Inspector,
    /// không phải đào vào code UI mỗi lần thêm kỹ năng như khảo cổ phần mềm.
    /// </summary>
    [GlobalClass]
    public partial class SkillTreeBranchData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string BranchId { get; set; } = "";
        [Export] public string BranchName { get; set; } = "Nhánh kỹ năng";
        [Export(PropertyHint.MultilineText)]
        public string Description { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }

        [ExportGroup("Nodes")]
        [Export] public Array<SkillTreeNodeData> Nodes { get; set; } = new();
    }
}
