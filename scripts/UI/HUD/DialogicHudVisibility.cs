using Godot;

namespace AshesofaDyingWorld.UI.HUD
{
    /// <summary>
    /// Ẩn HUD trong timeline Dialogic, rồi khôi phục ĐÚNG trạng thái cũ.
    /// Không can thiệp Dialogic timeline/portrait, không poll mỗi frame.
    /// Sử dụng cho các CanvasLayer gameplay (PartyHUD, menu và quest HUD).
    /// </summary>
    internal sealed class DialogicHudVisibility
    {
        private readonly CanvasLayer _layer;
        private Node _dialogic;
        private Callable _onStarted;
        private Callable _onEnded;
        private bool _isSuspended;
        private bool _visibleBeforeSuspension;

        public DialogicHudVisibility(CanvasLayer layer)
        {
            _layer = layer;
        }

        /// <summary>
        /// Lắng nghe trực tiếp signal của Dialogic; xử lý cả trường hợp scene
        /// được khởi tạo trong lúc một cuộc hội thoại đã bắt đầu.
        /// </summary>
        public void Bind()
        {
            _dialogic = _layer.GetNodeOrNull<Node>("/root/Dialogic");
            if (_dialogic == null)
                return;

            _onStarted = Callable.From(Suspend);
            _onEnded = Callable.From(Restore);
            _dialogic.Connect("timeline_started", _onStarted);
            _dialogic.Connect("timeline_ended", _onEnded);

            if (_dialogic.Get("current_timeline").VariantType != Variant.Type.Nil)
                Suspend();
        }

        private void Suspend()
        {
            if (_isSuspended)
                return;
            _isSuspended = true;
            _visibleBeforeSuspension = _layer.Visible;
            _layer.Visible = false;
        }

        private void Restore()
        {
            if (!_isSuspended)
                return;
            _isSuspended = false;
            _layer.Visible = _visibleBeforeSuspension;
        }

        /// <summary>Huỷ kết nối signal khi HUD rời scene để tránh callback rác.</summary>
        public void Unbind()
        {
            if (_dialogic != null && GodotObject.IsInstanceValid(_dialogic))
            {
                if (_dialogic.IsConnected("timeline_started", _onStarted))
                    _dialogic.Disconnect("timeline_started", _onStarted);
                if (_dialogic.IsConnected("timeline_ended", _onEnded))
                    _dialogic.Disconnect("timeline_ended", _onEnded);
            }
            _dialogic = null;
        }
    }
}
