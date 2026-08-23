using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    [Tool]
    public partial class EnvironmentTreeEditorPreview2D : StaticBody2D
    {
        [Export]
        public bool UseFullPreviewInEditor { get; set; } = true;

        public override void _EnterTree()
        {
            ApplyPreviewState();
        }

        public override void _Ready()
        {
            ApplyPreviewState();
            SetProcess(Engine.IsEditorHint());
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
            {
                ApplyPreviewState();
            }
        }

        private void ApplyPreviewState()
        {
            bool usePreview = Engine.IsEditorHint() && UseFullPreviewInEditor;
            SetItemVisible("EditorPreview", usePreview);
            SetItemVisible("Trunk", !usePreview);
            SetItemVisible("Canopy", !usePreview);
            SetItemVisible("AssetShadow", !usePreview);
        }

        private void SetItemVisible(string nodePath, bool visible)
        {
            CanvasItem item = GetNodeOrNull<CanvasItem>(nodePath);
            if (item != null)
            {
                item.Visible = visible;
            }
        }
    }
}
