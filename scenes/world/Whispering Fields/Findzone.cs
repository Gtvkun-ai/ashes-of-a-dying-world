using Godot;
using System;

public partial class Findzone : Area2D
{
	private Slime1 _slime;

	public override void _Ready()
	{
		_slime = GetParent().GetParent<Slime1>();
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	// BẠN ĐANG THIẾU CÁC HÀM NÀY:
	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_slime.StartChasing(body);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_slime.StopChasing();
		}
	}
}
