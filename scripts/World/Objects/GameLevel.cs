using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.World.Objects;
using AshesofaDyingWorld.Gameplay.Events;

namespace AshesofaDyingWorld.World.Maps
{
	public partial class GameLevel : Node2D
	{
		private readonly Dictionary<string, SpawnPoint> _spawnPoints = new();

		[Export] public bool UseManualCameraBounds { get; set; } = false;
		[Export] public Vector2 ManualCameraBoundsPosition { get; set; } = Vector2.Zero;
		[Export] public Vector2 ManualCameraBoundsSize { get; set; } = Vector2.Zero;
		[Export] public NodePath CameraBoundsSourcePath { get; set; }

		public override void _Ready()
		{
			var manager = GetTree().Root.GetNodeOrNull<SceneManager>("/root/SceneManager");
			manager?.OnSceneLoaded(this);

			GameplayEventBus.GetOrCreate(GetTree())?.Publish(new GameplayEvent(
				GameplayEventType.SceneEntered,
				targetId: string.IsNullOrWhiteSpace(SceneFilePath) ? Name.ToString() : SceneFilePath,
				sourceId: Name.ToString(),
				worldPosition: GlobalPosition,
				scenePath: SceneFilePath));
		}

		public void RegisterSpawnPoint(SpawnPoint point)
		{
			if (!string.IsNullOrEmpty(point.SpawnID) && !_spawnPoints.ContainsKey(point.SpawnID))
			{
				_spawnPoints.Add(point.SpawnID, point);
			}
		}

		public SpawnPoint GetSpawnPoint(string id)
		{
			_spawnPoints.TryGetValue(id, out var point);
			return point;
		}

		public bool TryGetCameraBounds(out Rect2 bounds)
		{
			if (UseManualCameraBounds && ManualCameraBoundsSize.X > 0 && ManualCameraBoundsSize.Y > 0)
			{
				bounds = new Rect2(ManualCameraBoundsPosition, ManualCameraBoundsSize);
				return true;
			}

			TileMapLayer tileMapLayer = ResolveCameraBoundsSource();
			if (tileMapLayer == null || tileMapLayer.TileSet == null)
			{
				bounds = default;
				return false;
			}

			Rect2I usedRect = tileMapLayer.GetUsedRect();
			if (usedRect.Size.X <= 0 || usedRect.Size.Y <= 0)
			{
				bounds = default;
				return false;
			}

			Vector2 tileSize = tileMapLayer.TileSet.TileSize;
			//Lấy tâm ô đầu tiên và ô cuối cùng
			Vector2 firstCellCenter = tileMapLayer.MapToLocal(usedRect.Position);
			Vector2 lastCellCenter = tileMapLayer.MapToLocal(usedRect.Position + usedRect.Size - Vector2I.One);
			//Đổi từ “tâm ô” → “góc ô” bằng cách trừ/cộng nửa 
			Vector2 localTopLeft = firstCellCenter - tileSize * 0.5f;
			Vector2 localBottomRight = lastCellCenter + tileSize * 0.5f;

			//Đổi sang tọa độ global: 
			Vector2 globalTopLeft = tileMapLayer.ToGlobal(localTopLeft);
			Vector2 globalBottomRight = tileMapLayer.ToGlobal(localBottomRight);
			Vector2 position = new(
				Mathf.Min(globalTopLeft.X, globalBottomRight.X),
				Mathf.Min(globalTopLeft.Y, globalBottomRight.Y));
			Vector2 end = new(
				Mathf.Max(globalTopLeft.X, globalBottomRight.X),
				Mathf.Max(globalTopLeft.Y, globalBottomRight.Y));
			bounds = new Rect2(position, end - position);
			return bounds.Size.X > 0 && bounds.Size.Y > 0;
		}

		private TileMapLayer ResolveCameraBoundsSource()
		{
			if (CameraBoundsSourcePath != null && !CameraBoundsSourcePath.IsEmpty)
			{
				TileMapLayer configuredLayer = GetNodeOrNull<TileMapLayer>(CameraBoundsSourcePath);
				if (configuredLayer != null)
				{
					return configuredLayer;
				}
			}

			return FindFirstTileMapLayer(this);
		}

		private TileMapLayer FindFirstTileMapLayer(Node node)
		{
			foreach (Node child in node.GetChildren())
			{
				if (child is TileMapLayer tileMapLayer)
				{
					return tileMapLayer;
				}

				TileMapLayer found = FindFirstTileMapLayer(child);
				if (found != null)
				{
					return found;
				}
			}

			return null;
		}
	}
}
