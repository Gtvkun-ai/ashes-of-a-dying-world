using Godot;

public partial class End : TextureButton
{
	private Tween _blinkTween;

	public override void _Ready()
	{
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		// 1) Hiệu ứng sáng-mờ
		_blinkTween?.Kill();
		_blinkTween = GetTree().CreateTween();
		_blinkTween.TweenProperty(this, "modulate",
								  new Color(1, 1, 1, 0.4f), 0.1f)
				   .SetTrans(Tween.TransitionType.Sine)
				   .SetEase(Tween.EaseType.InOut);
		_blinkTween.TweenProperty(this, "modulate",
								  new Color(1, 1, 1, 1f), 0.1f);

		// 2) Logic riêng của End (tùy bạn)
		// GetTree().Quit();
	}
}
