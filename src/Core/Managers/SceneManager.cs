using Godot;
using System.Linq;
using AshesofaDyingWorld.World.Objects;

public partial class SceneManager : Node
{
    public Player Player { get; set; }

    [Export] public string SceneDirPath = "res://scenes/world/Whispering Fields/";

    private string _targetSpawnID = "";

    public void SetPlayer(Player player)
    {
        Player = player;
    }

	// dùng để chuyển cảnh
    public void ChangeScene(string toSceneName, string targetSpawnID)
    {
        if (Player == null) return;

        if (Player.GetParent() != null)
        {
            Player.GetParent().RemoveChild(Player);
        }

        _targetSpawnID = targetSpawnID;
        
        toSceneName = toSceneName.Trim();
        var fullPath = $"{SceneDirPath}{toSceneName}.tscn";

        if (!ResourceLoader.Exists(fullPath))
        {
            GD.PushError($"Scene file missing: {fullPath}");
            return;
        }

        GetTree().ChangeSceneToFile(fullPath);
    }
    
	// hàm được gọi khi cảnh mới đã sẵn sàng
    public void OnSceneReady(Node newSceneRoot)
    {
        if(Player == null) return;
        
        newSceneRoot.AddChild(Player);
		// Tìm SpawnPoint theo ID trong cảnh mới
        SpawnPoint targetPoint = FindSpawnPoint(newSceneRoot, _targetSpawnID);

        if(targetPoint != null)
        {
            Player.GlobalPosition = targetPoint.GlobalPosition;
            GD.Print($"Player spawned at point ID: {_targetSpawnID}");
        }
        else
        {
            GD.PrintErr($"SpawnPoint with ID '{_targetSpawnID}' not found in the new scene.");
            Player.GlobalPosition = Vector2.Zero;
        }
    }

    private SpawnPoint FindSpawnPoint(Node root, string id)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is SpawnPoint spawn && spawn.SpawnID == id)
            {
                return spawn;
            }
            
            // Tìm kiếm đệ quy trong các con
            var found = FindSpawnPoint(child, id);
            if (found != null) return found;
        }
        return null;
    }

    public void OnSceneLoaded(Node newSceneRoot)
    {
        if (Player == null) return;
        
        if (newSceneRoot is AshesofaDyingWorld.World.Maps.GameLevel level)
        {
            level.AddChild(Player);
            var targetPoint = level.GetSpawnPoint(_targetSpawnID);

            if (targetPoint != null)
            {
                Player.GlobalPosition = targetPoint.GlobalPosition;
            }
            else 
            {
                Player.GlobalPosition = Vector2.Zero;
            }
        }
    }	
}