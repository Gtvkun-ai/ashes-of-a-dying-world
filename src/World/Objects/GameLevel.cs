using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.World.Objects;

namespace AshesofaDyingWorld.World.Maps
{
    public partial class GameLevel : Node2D 
    {
        private Dictionary<string, SpawnPoint> _spawnPoints = new();

        public override void _Ready()
        {
            // ✅ SỬA LẠI ĐƯỜNG DẪN
            var manager = GetTree().Root.GetNodeOrNull<SceneManager>("/root/SceneManager");
            manager?.OnSceneLoaded(this);
        }

        public void RegisterSpawnPoint(SpawnPoint point)
        {
            if (!string.IsNullOrEmpty(point.SpawnID) && !_spawnPoints.ContainsKey(point.SpawnID))
            {
                _spawnPoints.Add(point.SpawnID, point);
                GD.Print($"Registered spawn point: {point.SpawnID}");
            }
        }

        public SpawnPoint GetSpawnPoint(string id)
        {
            _spawnPoints.TryGetValue(id, out var point);
            return point;
        }
    }
}