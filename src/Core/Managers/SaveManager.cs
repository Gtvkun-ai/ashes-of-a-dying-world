using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.Menus;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class SaveManager : Node
    {
        public static SaveManager Instance { get; private set; }

        private const string SavePath = "user://savegame.json";
        private const string MainScenePath = "res://scenes/main/screen_main.tscn";
        private const Key QuickSaveKey = Key.S;
        private const Key QuickLoadKey = Key.L;

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

            if (MatchesShortcut(keyEvent, QuickSaveKey, requireCtrl: true))
            {
                SaveGame();
                GetViewport().SetInputAsHandled(); // Ngăn chặn sự kiện tiếp tục nếu đã xử lý lưu game
                return;
            }

            if (MatchesShortcut(keyEvent, QuickLoadKey, requireCtrl: true))
            {
                _ = LoadGameAsync();
                GetViewport().SetInputAsHandled();  // Ngăn chặn sự kiện tiếp tục nếu đã xử lý tải game
            }
        }

        private static bool MatchesShortcut(
            InputEventKey keyEvent,
            Key key,
            bool requireCtrl = false,
            bool requireShift = false,
            bool requireAlt = false)
        {
            if (keyEvent.Keycode != key)
            {
                return false;
            }

            return keyEvent.CtrlPressed == requireCtrl
                && keyEvent.ShiftPressed == requireShift
                && keyEvent.AltPressed == requireAlt;
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
            try
            {
                SaveGameData snapshot = LoadSnapshot();
                if (snapshot == null)
                {
                    return Error.FileNotFound;
                }

                SceneTree tree = GetTree();
                if (tree == null)
                {
                    return Error.DoesNotExist;
                }

                if (!ResourceLoader.Exists(MainScenePath))
                {
                    GD.PrintErr($"[SaveManager] Main scene is missing: {MainScenePath}");
                    return Error.FileNotFound;
                }

                SceneManager sceneManager = tree.Root.GetNodeOrNull<SceneManager>("SceneManager");
                if (sceneManager == null)
                {
                    GD.PrintErr("[SaveManager] Cannot load because SceneManager is missing.");
                    return Error.DoesNotExist;
                }

                tree.Paused = false;
                tree.Root?.GuiReleaseFocus();

                PlayerManager.Instance?.ResetParty();
                sceneManager.SetPlayer(null);

                Error sceneChangeError = tree.ChangeSceneToFile(MainScenePath);
                if (sceneChangeError != Error.Ok)
                {
                    return sceneChangeError;
                }

                const int maxFramesToWait = 30;
                Node currentScene = null;
                Button loginButton = null;

                for (int i = 0; i < maxFramesToWait; i++)
                {
                    await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                    currentScene = tree.CurrentScene;
                    if (currentScene == null)
                    {
                        continue;
                    }

                    loginButton = currentScene.GetNodeOrNull<Button>("login");
                    if (loginButton != null)
                    {
                        break;
                    }
                }

                if (currentScene == null)
                {
                    GD.PrintErr("[SaveManager] Main scene failed to load after waiting.");
                    return Error.DoesNotExist;
                }

                if (loginButton == null)
                {
                    GD.PrintErr("[SaveManager] Login button not found on main scene after waiting.");
                    return Error.DoesNotExist;
                }

                loginButton.EmitSignal(Button.SignalName.Pressed);
                GD.Print("[SaveManager] Ctrl+L returned to main and auto-pressed login.");
                return Error.Ok;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Load failed with exception: {ex}");
                return Error.Failed;
            }
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

            // Đảm bảo người chơi không bị kẹt trạng thái sau khi load (đang attack/knockback/pause).
            player.ResetTransientStateAfterLoad();

            SceneTree tree = GetTree();
            if (tree != null)
            {
                tree.Paused = false;
                tree.Root?.GuiReleaseFocus();

                Node currentScene = tree.CurrentScene;
                if (currentScene != null)
                {
                    currentScene.GetNodeOrNull<GameMenuButton>("GameMenuButton")?.ResetUiStateAfterLoad();
                }
            }
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
