using Godot;
using System;

public partial class Attackzone : Area2D
{
	// Đổi 'slime' thành 'Slime1' để khớp với script chính
	private Slime1 _slime; 

	public override void _Ready()
	{
		_slime = GetParent<Slime1>();
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_slime.Attack();
		}
	}
}
