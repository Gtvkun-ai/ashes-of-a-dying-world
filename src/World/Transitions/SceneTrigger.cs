using Godot;

public partial class SceneTrigger : Area2D
{
    [Export] public string ConnectedScene { get; set; } = "";
    [Export] public string TargetSpawnID { get; set; } = ""; // ✅ Đổi tên

    private SceneManager _sceneManager;
    private bool _isChangingScene = false;

    public override void _Ready()
    {
        _sceneManager = GetTree().Root.GetNodeOrNull<SceneManager>("/root/SceneManager");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Player && !string.IsNullOrEmpty(ConnectedScene) && !_isChangingScene)
        {
            _isChangingScene = true;
            
            _sceneManager?.ChangeScene(ConnectedScene, TargetSpawnID);
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body is Player)
        {
            _isChangingScene = false;
        }
    }
}