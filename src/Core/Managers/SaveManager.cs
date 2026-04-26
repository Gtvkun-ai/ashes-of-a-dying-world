using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class SaveManager : Node
    {
        public static SaveManager Instance { get; private set; }

        private const string SavePath = "user://savegame.json";
        private const Key QuickSaveKey = Key.F5;
        private const Key QuickLoadKey = Key.F9;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (keyEvent.Keycode == QuickSaveKey)
            {
                SaveGame();
                GetViewport().SetInputAsHandled(); // Ngăn chặn sự kiện tiếp tục nếu đã xử lý lưu game
                return;
            }

            if (keyEvent.Keycode == QuickLoadKey)
            {
                _ = LoadGameAsync();
                GetViewport().SetInputAsHandled();  // Ngăn chặn sự kiện tiếp tục nếu đã xử lý tải game
            }
        }

        public bool HasSaveGame()
        {
            return FileAccess.FileExists(SavePath);
        }

        public SaveGameData LoadSnapshot()
        {
            if (!HasSaveGame())
            {
                return null;
            }

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read); // Sử dụng 'using' để đảm bảo file được đóng sau khi đọc
            if (file == null)
            {
                GD.PrintErr($"[SaveManager] Cannot open save for reading: {SavePath}");
                return null;
            }

            string json = file.GetAsText();
            if (string.IsNullOrWhiteSpace(json))
            {
                GD.PrintErr("[SaveManager] Save file is empty.");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SaveGameData>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Failed to parse save file: {ex.Message}");
                return null;
            }
        }

        // Phương thức lưu game, trả về Error.Ok nếu thành công hoặc lỗi cụ thể nếu thất bại
        public Error SaveGame()
        {
            SaveGameData snapshot = CaptureCurrentGame(); // Chụp lại trạng thái hiện tại của game để lưu
            if (snapshot == null)
            {
                return Error.Failed;
            }

            string json;
            try
            {
                json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Failed to serialize save data: {ex.Message}");
                return Error.Failed;
            }

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write); // Sử dụng 'using' để đảm bảo file được đóng sau khi ghi
            if (file == null)
            {
                Error openError = FileAccess.GetOpenError();
                GD.PrintErr($"[SaveManager] Cannot open save for writing: {openError}");
                return openError;
            }

            file.StoreString(json);
            file.Flush();
            GD.Print($"[SaveManager] Saved game to {ProjectSettings.GlobalizePath(SavePath)}");
            return Error.Ok;
        }

        public async Task<Error> LoadGameAsync()
        {
            SaveGameData snapshot = LoadSnapshot();
            if (snapshot == null)
            {
                return Error.FileNotFound;
            }

            SceneManager sceneManager = GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager");
            if (sceneManager?.Player == null)
            {
                GD.PrintErr("[SaveManager] Cannot load because SceneManager.Player is missing.");
                return Error.DoesNotExist;
            }

            string currentScenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty;
            string targetScenePath = snapshot.ScenePath ?? string.Empty;

            if (!string.IsNullOrEmpty(targetScenePath) &&
                !string.Equals(currentScenePath, targetScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Error sceneError = await sceneManager.ChangeSceneToPathAsync(
                    targetScenePath,
                    snapshot.PlayerPosition?.ToVector2());

                if (sceneError != Error.Ok)
                {
                    return sceneError;
                }
            }

            ApplyLoadedGame(sceneManager.Player, snapshot);
            sceneManager.EnsureWorldUi(GetTree().CurrentScene);
            GD.Print("[SaveManager] Load completed.");
            return Error.Ok;
        }

        public void ApplyLoadedGame(Player player, SaveGameData snapshot)
        {
            if (player == null || snapshot?.Player == null)
            {
                return;
            }

            PlayerSaveData playerData = snapshot.Player;
            PlayerStats stats = player.GetStatsNode();
            if (stats == null)
            {
                GD.PrintErr("[SaveManager] PlayerStats is missing; cannot apply save.");
                return;
            }

            if (!string.IsNullOrEmpty(playerData.CharacterConfigPath))
            {
                CharacterConfig config = GD.Load<CharacterConfig>(playerData.CharacterConfigPath);
                if (config != null)
                {
                    stats.SetCharacterConfig(config);
                }
            }

            stats.SetCurrentLevel(playerData.Level);

            InventoryManager inventory = player.GetInventoryManager();
            inventory?.RestoreItems(playerData.InventoryItemPaths);

            EquipmentManager equipment = player.GetEquipmentManager();
            equipment?.RestoreEquipment(playerData.EquippedItems);

            player.RestoreSavedSkills(playerData.ActiveSkills, playerData.SkillCooldowns, playerData.ActiveTimedSkill);

            stats.RestoreResourceValues(
                playerData.CurrentHP,
                playerData.CurrentMP,
                playerData.CurrentStamina);

            if (snapshot.PlayerPosition != null)
            {
                player.GlobalPosition = snapshot.PlayerPosition.ToVector2();
            }

            PlayerManager.Instance?.SetActiveCharacter(snapshot.ActiveCharacterIndex);
        }

        private SaveGameData CaptureCurrentGame()
        {
            SceneManager sceneManager = GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager");
            Player player = sceneManager?.Player;
            PlayerStats stats = player?.GetStatsNode();

            if (player == null || stats == null)
            {
                GD.PrintErr("[SaveManager] Cannot save because player or PlayerStats is missing.");
                return null;
            }

            InventoryManager inventory = player.GetInventoryManager();
            EquipmentManager equipment = player.GetEquipmentManager();

            return new SaveGameData
            {
                Version = 1,
                SavedAtUtc = DateTime.UtcNow.ToString("O"),
                ScenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty,
                PlayerPosition = Vector2SaveData.FromVector2(player.GlobalPosition),
                ActiveCharacterIndex = PlayerManager.Instance?.ActiveCharacterIndex ?? 0,
                Player = new PlayerSaveData
                {
                    CharacterConfigPath = stats.ConfigData?.ResourcePath ?? string.Empty,
                    CharacterId = stats.ConfigData?.ID ?? string.Empty,
                    Level = stats.CurrentLevel,
                    CurrentHP = stats.CurrentHP,
                    CurrentMP = stats.CurrentMP,
                    CurrentStamina = stats.CurrentStamina,
                    InventoryItemPaths = inventory?.GetItemResourcePaths() ?? new(),
                    EquippedItems = equipment?.CaptureEquippedItems() ?? new(),
                    ActiveSkills = player.CaptureActiveSkills(),
                    SkillCooldowns = player.CaptureSkillCooldowns(),
                    ActiveTimedSkill = player.CaptureActiveTimedSkill()
                }
            };
        }
    }
}
