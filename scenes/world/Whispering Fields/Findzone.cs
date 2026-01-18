using Godot;
using System;

public partial class Findzone : Area2D
{
	private Slime1 _slime;

	public override void _Ready()
	{
		// CÁCH SỬA AN TOÀN:
		// Dòng này sẽ lấy node cha trực tiếp. 
		// Yêu cầu trong Editor: Findzone phải là con trực tiếp của Slime1.
		_slime = GetParentOrNull<Slime1>();

		// Nếu không tìm thấy ở cha trực tiếp, thử tìm ở cấp "ông nội" (nếu bạn lỡ dùng Node trung gian)
		if (_slime == null)
		{
			_slime = GetParent().GetParent() as Slime1;
		}

		// Nếu vẫn không thấy thì báo lỗi đỏ lòm để biết đường sửa
		if (_slime == null)
		{
			GD.PrintErr("LỖI NGIÊM TRỌNG: Findzone không tìm thấy script Slime1 ở cha hoặc ông nội!");
		}

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		// In ra tên vật thể va chạm để kiểm tra
		GD.Print("Có vật đi vào vùng phát hiện: " + body.Name);

		if (body.IsInGroup("Player"))
		{
			GD.Print(">>> ĐÚNG LÀ PLAYER! BẮT ĐẦU ĐUỔI!"); // Dòng này hiện ra thì Slime mới chạy
			
			if (_slime != null)
			{
				_slime.StartChasing(body);
			}
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			GD.Print("<<< Player đã chạy thoát.");
			
			if (_slime != null)
			{
				_slime.StopChasing();
			}
		}
	}
}
