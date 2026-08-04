using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Một nút trên cây kỹ năng.
    /// SkillData chỉ mô tả kỹ năng chiến đấu; lớp này bổ sung vị trí hiển thị
    /// và điều kiện mở khóa dành riêng cho panel phát triển kỹ năng.
    /// </summary>
    [GlobalClass]
    public partial class SkillTreeNodeData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string NodeId { get; set; } = "";
        [Export] public SkillData Skill { get; set; }

        [ExportGroup("Graph")]
        // Tọa độ của góc trên bên trái node trong vùng cây kỹ năng.
        [Export] public Vector2 GraphPosition { get; set; } = Vector2.Zero;

        [ExportGroup("Unlock Rules")]
        // Các NodeId phải được mở trước. Một node có thể phụ thuộc nhiều nhánh.
        [Export] public Array<string> RequiredNodeIds { get; set; } = new();
        [Export(PropertyHint.Range, "1,99,1")]
        public int RequiredCharacterLevel { get; set; } = 1;
        [Export(PropertyHint.Range, "0,20,1")]
        public int SkillPointCost { get; set; } = 1;

        // Node khởi đầu được cấp miễn phí và không tiêu điểm kỹ năng.
        [Export] public bool GrantedByDefault { get; set; } = false;
    }
}
