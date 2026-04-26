using Godot;
using System.Threading.Tasks;
using AshesofaDyingWorld.World.Objects;

public partial class SceneManager : Node
{
    private const string PartyHUDPath = "res://scenes/ui/PartyHUD.tscn";
    private const string GameMenuPath = "res://scenes/ui/GameMenuButton.tscn";

    public Player Player { get; set; }

    [Export] public string SceneDirPath = "res://scenes/world/WhisperingFields/";

    private string _targetSpawnID = "";
    private bool _restoreExactPosition = false;
    private Vector2 _targetPlayerPosition = Vector2.Zero;
    private bool _isSceneChangeInProgress = false;

    public void SetPlayer(Player player)
    {
        Player = player;
    }

    public void ChangeScene(string toSceneName, string targetSpawnID)
    {
        toSceneName = toSceneName.Trim();
        var fullPath = $"{SceneDirPath}{toSceneName}.tscn";

        if (!ResourceLoader.Exists(fullPath))
        {
            GD.PushError($"Scene file missing: {fullPath}");
            return;
        }

        _ = ChangeSceneToPathAsync(fullPath, null, targetSpawnID);
    }

    public async Task<Error> ChangeSceneToPathAsync(string scenePath, Vector2? playerPosition = null, string targetSpawnID = "")
    {
        if (Player == null)
        {
            return Error.DoesNotExist;
        }

        if (_isSceneChangeInProgress)
        {
            return Error.Failed;
        }

        if (string.IsNullOrEmpty(scenePath) || !ResourceLoader.Exists(scenePath))
        {
            GD.PushError($"Scene file missing: {scenePath}");
            return Error.FileNotFound;
        }

        _isSceneChangeInProgress = true;

        Node previousParent = Player.GetParent();
        previousParent?.RemoveChild(Player);

        _targetSpawnID = targetSpawnID ?? string.Empty;
        _restoreExactPosition = playerPosition.HasValue;
        _targetPlayerPosition = playerPosition ?? Vector2.Zero;

        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            previousParent?.AddChild(Player);
            _isSceneChangeInProgress = false;
            return error;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Node newSceneRoot = GetTree().CurrentScene;
        if (newSceneRoot == null)
        {
            _isSceneChangeInProgress = false;
            return Error.DoesNotExist;
        }

        OnSceneReady(newSceneRoot);
        _isSceneChangeInProgress = false;
        return Error.Ok;
    }

    public void OnSceneReady(Node newSceneRoot)
    {
        if (Player == null || newSceneRoot == null)
        {
            return;
        }

        if (Player.GetParent() == newSceneRoot &&
            !_restoreExactPosition &&
            string.IsNullOrEmpty(_targetSpawnID))
        {
            EnsureWorldUi(newSceneRoot);
            return;
        }

        if (Player.GetParent() != newSceneRoot)
        {
            newSceneRoot.AddChild(Player);
        }
        EnsureWorldUi(newSceneRoot);

        if (_restoreExactPosition)
        {
            Player.GlobalPosition = _targetPlayerPosition;
            _restoreExactPosition = false;
            _targetSpawnID = "";
            return;
        }

        SpawnPoint targetPoint = FindSpawnPoint(newSceneRoot, _targetSpawnID);
        if (targetPoint != null)
        {
            Player.GlobalPosition = targetPoint.GlobalPosition;
            GD.Print($"Player spawned at point ID: {_targetSpawnID}");
        }
        else
        {
            GD.PrintErr($"SpawnPoint with ID '{_targetSpawnID}' not found in the new scene.");
            Player.GlobalPosition = Vector2.Zero;
        }

        _targetSpawnID = "";
    }

    private SpawnPoint FindSpawnPoint(Node root, string id)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is SpawnPoint spawn && spawn.SpawnID == id)
            {
                return spawn;
            }

            var found = FindSpawnPoint(child, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public void EnsureWorldUi(Node sceneRoot)
    {
        if (sceneRoot == null)
        {
            return;
        }

        EnsureOverlay(sceneRoot, "PartyHUD", PartyHUDPath);
        EnsureOverlay(sceneRoot, "GameMenuButton", GameMenuPath);
    }

    private void EnsureOverlay(Node sceneRoot, string nodeName, string packedScenePath)
    {
        if (sceneRoot.GetNodeOrNull(nodeName) != null)
        {
            return;
        }

        PackedScene packedScene = GD.Load<PackedScene>(packedScenePath);
        if (packedScene == null)
        {
            GD.PrintErr($"[SceneManager] Failed to load overlay scene: {packedScenePath}");
            return;
        }

        sceneRoot.AddChild(packedScene.Instantiate());
    }

    public void OnSceneLoaded(Node newSceneRoot)
    {
        OnSceneReady(newSceneRoot);
    }
}
