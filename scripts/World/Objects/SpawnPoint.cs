using Godot;
using AshesofaDyingWorld.World.Maps;

namespace AshesofaDyingWorld.World.Objects;

public partial class SpawnPoint : Marker2D
{
    [Export] public string SpawnID { get; set; } = "";

    public override void _EnterTree()
    {
        // Tìm node cha là GameLevel để đăng ký
        // Dùng owner hoặc tìm ngược lên
        var level = GetLevelNode();
        if (level != null)
        {
            level.RegisterSpawnPoint(this);
        }
    }

    private GameLevel GetLevelNode()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is GameLevel level) return level;
            current = current.GetParent();
        }
        return null;
    }
}