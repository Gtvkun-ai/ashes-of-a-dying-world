using Godot;
using System;

public partial class SceneManager : Node
{
	public Player Player { get; set; }

	[Export] public string SceneDirPath = "res://scenes/world/Whispering Fields/";

	// Lưu scene trước đó để biết spawn ở đâu
	public string PreviousScene { get; private set; } = "";

	public void SetPlayer(Player player)
	{
		Player = player;
	}

	public void ChangeScene(Node from, string toSceneName)
	{
		if (Player == null)
		{
			GD.PushError("Player not set in SceneManager.");
			return;
		}

		if (Player.GetParent() == null)
		{
			GetTree().Root.AddChild(Player);
		}

		if (Player.GetParent() != null)
			Player.GetParent().RemoveChild(Player);

		// Lưu tên scene hiện tại trước khi chuyển
		var currentSceneName = GetTree().CurrentScene?.Name ?? "";
		PreviousScene = currentSceneName;

		toSceneName = toSceneName.Trim();
		var fullPath = $"{SceneDirPath}{toSceneName}.tscn";

		GD.Print($"Changing from '{PreviousScene}' to '{toSceneName}'");

		if (!ResourceLoader.Exists(fullPath))
		{
			GD.PushError($"Scene file missing: {fullPath}");
			return;
		}

		GetTree().CallDeferred("change_scene_to_file", fullPath);
	}
}