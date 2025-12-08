using Godot;

public partial class SceneTrigger : Area2D
{
	[Export] 
	public string ConnectedScene { get; set; } = "";
	
	[Export] 
	public string SpawnPointName { get; set; } = "SpawnPoint";

	private SceneManager _sceneManager;
	private bool _isChangingScene = false;

	public override void _Ready()
	{
		_sceneManager = GetTree().Root.GetNode<SceneManager>("SceneManager");
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body is Player && !string.IsNullOrEmpty(ConnectedScene) && !_isChangingScene)
		{
			_isChangingScene = true;
			
			// Lưu tên spawn point vào SceneManager
			_sceneManager.Set("TargetSpawnPoint", SpawnPointName);
			
			_sceneManager.ChangeScene(GetTree().CurrentScene, ConnectedScene);
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