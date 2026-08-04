using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.UI.HUD
{
	public partial class DamageNumberService : CanvasLayer
	{
		private class DamageNumber
		{
			public Label Label;
			public Vector2 Velocity;
			public float Lifetime;
			public float Age;
		}

		public static DamageNumberService Instance { get; private set; }

		[Export] public Vector2 ScreenOffset { get; set; } = new(0f, -42f);
		[Export] public Vector2 FloatVelocity { get; set; } = new(0f, -42f);
		[Export] public float Lifetime { get; set; } = 0.8f;
		[Export] public int FontSize { get; set; } = 18;
		[Export] public Color DamageColor { get; set; } = new Color(1f, 0.28f, 0.12f);

		private readonly List<DamageNumber> _numbers = new();

		public override void _Ready()
		{
			if (Instance != null && Instance != this)
			{
				QueueFree();
				return;
			}

			Instance = this;
			Layer = 50;
		}

		public override void _ExitTree()
		{
			if (Instance == this)
			{
				Instance = null;
			}
		}

		public static DamageNumberService GetOrCreate(SceneTree tree)
		{
			if (Instance != null && GodotObject.IsInstanceValid(Instance))
			{
				return Instance;
			}

			if (tree?.Root == null)
			{
				return null;
			}

			var existing = tree.Root.GetNodeOrNull<DamageNumberService>("DamageNumberService");
			if (existing != null)
			{
				return existing;
			}

			var service = new DamageNumberService
			{
				Name = "DamageNumberService"
			};
			tree.Root.AddChild(service);
			return service;
		}

		public void ShowDamage(Node2D source, float amount)
		{
			if (source == null || amount <= 0f)
			{
				return;
			}

			var label = new Label
			{
				Text = Mathf.RoundToInt(amount).ToString(),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				TopLevel = true,
				ZIndex = 200,
				Modulate = DamageColor
			};
			label.AddThemeFontSizeOverride("font_size", FontSize);
			label.AddThemeColorOverride("font_color", DamageColor);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 4);
			AddChild(label);

			Vector2 screenPos = source.GetGlobalTransformWithCanvas().Origin + ScreenOffset;
			Vector2 labelSize = label.GetCombinedMinimumSize();
			label.Position = screenPos - new Vector2(labelSize.X * 0.5f, labelSize.Y);

			_numbers.Add(new DamageNumber
			{
				Label = label,
				Velocity = FloatVelocity,
				Lifetime = Mathf.Max(0.1f, Lifetime),
				Age = 0f
			});
		}

		public override void _Process(double delta)
		{
			for (int i = _numbers.Count - 1; i >= 0; i--)
			{
				var number = _numbers[i];
				if (!GodotObject.IsInstanceValid(number.Label))
				{
					_numbers.RemoveAt(i);
					continue;
				}

				number.Age += (float)delta;
				float progress = Mathf.Clamp(number.Age / number.Lifetime, 0f, 1f);
				number.Label.Position += number.Velocity * (float)delta;
				number.Label.Modulate = new Color(DamageColor.R, DamageColor.G, DamageColor.B, 1f - progress);

				if (progress >= 1f)
				{
					number.Label.QueueFree();
					_numbers.RemoveAt(i);
				}
			}
		}
	}
}
