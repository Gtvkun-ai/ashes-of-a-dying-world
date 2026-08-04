using Godot;
using System;
using System.Threading.Tasks;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.World.Maps;
using AshesofaDyingWorld.World.Objects;

public partial class SceneManager : Node
{
    private const string PartyHUDPath = "res://scenes/ui/hud/party_hud.tscn";
    private const string GameMenuPath = "res://scenes/ui/menus/game_menu_button.tscn";
    private const string DefaultPlayerScenePath = "res://scenes/characters/player/player.tscn";
    private static readonly Vector2 DefaultCameraZoom = new(2f, 2f);

    public Player Player { get; set; }

    [Export] public string SceneDirPath = "res://scenes/world/whispering_fields/";

    private string _targetSpawnID = "";
    private bool _restoreExactPosition = false; // Đánh dấu nếu cần khôi phục vị trí chính xác thay vì tìm spawn point
    private Vector2 _targetPlayerPosition = Vector2.Zero;
    private bool _isSceneChangeInProgress = false;

    public void SetPlayer(Player player)
    {
        Player = player;
    }

    public void ChangeScene(string toSceneName, string targetSpawnID)
    {
        toSceneName = NormalizeSceneName(toSceneName);
        var fullPath = $"{SceneDirPath}{toSceneName}.tscn";

        if (!ResourceLoader.Exists(fullPath))
        {
            GD.PushError($"Scene file missing: {fullPath}");
            return;
        }

        _ = ChangeSceneToPathAsync(fullPath, null, targetSpawnID);
    }

    private static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return string.Empty;
        }

        string trimmedName = sceneName.Trim();
        return trimmedName.Equals("Field1", StringComparison.OrdinalIgnoreCase)
            ? "field_01"
            : trimmedName;
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

        Node previousParent = Player.GetParent(); // lấy parrent hiện tại của player trước khi chuyển scene
        previousParent?.RemoveChild(Player); // tách player

        // Đặt thông tin spawn mục tiêu và vị trí khôi phục dựa trên tham số truyền vào
        _targetSpawnID = targetSpawnID ?? string.Empty;
        _restoreExactPosition = playerPosition.HasValue; // nếu có vị trí cụ thể thì đánh dấu khôi phục vị trí chính xác, nếu không sẽ tìm spawn point
        _targetPlayerPosition = playerPosition ?? Vector2.Zero;

        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            previousParent?.AddChild(Player); // nếu lỗi thì gắn player về parrent cũ
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

    public async Task<Error> ReloadSceneWithFreshPlayerAsync(string scenePath, Vector2? playerPosition = null)
    {
        if (_isSceneChangeInProgress)
        {
            return Error.Failed;
        }

        if (string.IsNullOrEmpty(scenePath) || !ResourceLoader.Exists(scenePath))
        {
            GD.PushError($"Scene file missing: {scenePath}");
            return Error.FileNotFound;
        }

        string playerScenePath = ResolvePlayerScenePath(); // Cố gắng lấy đường dẫn scene của player hiện tại, nếu không tồn tại thì dùng mặc định
        PackedScene playerScene = GD.Load<PackedScene>(playerScenePath);
        if (playerScene == null)
        {
            GD.PrintErr($"[SceneManager] Failed to load player scene: {playerScenePath}");
            return Error.FileNotFound;
        }

        _isSceneChangeInProgress = true;
        _targetSpawnID = string.Empty;
        _restoreExactPosition = false; // Đánh dấu không khôi phục vị trí chính xác, sẽ tìm spawn point thay vì đặt trực tiếp
        _targetPlayerPosition = playerPosition ?? Vector2.Zero;

        PlayerManager.Instance?.ResetParty();
        Player = null;

        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
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

        Player newPlayer = playerScene.Instantiate<Player>();
        if (newPlayer == null)
        {
            GD.PrintErr($"[SceneManager] Failed to instantiate player scene: {playerScenePath}");
            _isSceneChangeInProgress = false;
            return Error.CantCreate;
        }

        newSceneRoot.AddChild(newPlayer);
        SetPlayer(newPlayer);

        if (playerPosition.HasValue)
        {
            newPlayer.GlobalPosition = playerPosition.Value;
        }

        EnsureWorldUi(newSceneRoot);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        ConfigurePlayerCamera(newSceneRoot);

        _isSceneChangeInProgress = false;
        return Error.Ok;
    }

    public void OnSceneReady(Node newSceneRoot)
    {
        if (Player == null || newSceneRoot == null)
        {
            return;
        }

        bool playerAlreadyAttached = Player.GetParent() == newSceneRoot;

        if (!playerAlreadyAttached)
        {
            newSceneRoot.AddChild(Player);
        }

        EnsureWorldUi(newSceneRoot);

        // player đã đúng scene và không cần khôi phục vị trí chính xác, cũng không có spawn point cụ thể nào được chỉ định,
        //  nên giữ nguyên vị trí hiện tại và chỉ cần đảm bảo camera hoạt động đúng
        if (playerAlreadyAttached &&
            !_restoreExactPosition &&
            string.IsNullOrEmpty(_targetSpawnID))
        {
            ConfigurePlayerCamera(newSceneRoot);
            return;
        }

        // Nếu cần khôi phục vị trí chính xác, ưu tiên đặt player về vị trí đó
        if (_restoreExactPosition)
        {
            Player.GlobalPosition = _targetPlayerPosition;
            _restoreExactPosition = false;
            _targetSpawnID = "";
            ConfigurePlayerCamera(newSceneRoot);
            return;
        }

        SpawnPoint targetPoint = FindSpawnPoint(newSceneRoot, _targetSpawnID);
        if (targetPoint != null)
        {
            Player.GlobalPosition = targetPoint.GlobalPosition;
        }
        else
        {
            GD.PrintErr($"SpawnPoint with ID '{_targetSpawnID}' not found in the new scene.");
            Player.GlobalPosition = Vector2.Zero;
        }

        _targetSpawnID = "";
        ConfigurePlayerCamera(newSceneRoot);
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

    // Phương thức này có thể được gọi từ các scene khác để khởi tạo player khi bắt đầu game hoặc load game
    public void EnsureWorldUi(Node sceneRoot)
    {
        if (sceneRoot == null)
        {
            return;
        }

        EnsureOverlay(sceneRoot, "PartyHUD", PartyHUDPath); 
        EnsureOverlay(sceneRoot, "GameMenuButton", GameMenuPath);
    }

    //  Phương thức này có thể được gọi khi bắt đầu game mới hoặc load game để đảm bảo player được tạo mới và đặt vào scene đúng cách
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

        try
        {
            Node overlay = packedScene.Instantiate(); // Cố gắng tạo instance của scene
            if (overlay == null)
            {
                GD.PrintErr($"[SceneManager] Overlay scene instantiated as null: {packedScenePath}");
                return;
            }

            sceneRoot.AddChild(overlay);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneManager] Failed to instantiate overlay '{packedScenePath}': {ex.Message}");
        }
    }

    public void OnSceneLoaded(Node newSceneRoot)
    {
        OnSceneReady(newSceneRoot);
    }

    public void ConfigurePlayerCamera(Node sceneRoot)
    {
        if (Player == null)
        {
            return;
        }

        Camera2D camera = Player.GetNodeOrNull<Camera2D>("follow");
        if (camera == null)
        {
            GD.PrintErr("[SceneManager] Player camera 'follow' not found.");
            return;
        }

        camera.Enabled = true;
        camera.Zoom = DefaultCameraZoom;
        ApplyCameraLimits(camera, sceneRoot);
        camera.CallDeferred("make_current");
    }

    private void ApplyCameraLimits(Camera2D camera, Node sceneRoot)
    {
        if (camera == null)
        {
            return;
        }

        GameLevel level = sceneRoot as GameLevel ?? FindGameLevel(sceneRoot);
        if (level == null || !level.TryGetCameraBounds(out Rect2 bounds))
        {
            camera.LimitLeft = -10000000;
            camera.LimitTop = -10000000;
            camera.LimitRight = 10000000;
            camera.LimitBottom = 10000000;
            return;
        }

        camera.LimitLeft = Mathf.RoundToInt(bounds.Position.X);
        camera.LimitTop = Mathf.RoundToInt(bounds.Position.Y);
        camera.LimitRight = Mathf.RoundToInt(bounds.End.X);
        camera.LimitBottom = Mathf.RoundToInt(bounds.End.Y);
    }

    private GameLevel FindGameLevel(Node node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is GameLevel level)
        {
            return level;
        }

        foreach (Node child in node.GetChildren())
        {
            GameLevel found = FindGameLevel(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // Phương thức này cố gắng lấy đường dẫn scene của player hiện tại nếu có, nếu không tồn tại hoặc không hợp lệ thì trả về đường dẫn mặc định
    private string ResolvePlayerScenePath()
    {
        if (Player != null && GodotObject.IsInstanceValid(Player))
        {
            string sceneFilePath = Player.SceneFilePath;
            if (!string.IsNullOrEmpty(sceneFilePath) && ResourceLoader.Exists(sceneFilePath))
            {
                return sceneFilePath;
            }
        }

        return DefaultPlayerScenePath;
    }
}
