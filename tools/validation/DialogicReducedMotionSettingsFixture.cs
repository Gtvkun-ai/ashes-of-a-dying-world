using Godot;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.Tools.Validation
{
    public partial class DialogicReducedMotionSettingsFixture : SettingsManager
    {
        public override void _EnterTree()
        {
            CurrentSettings.ReducedMotion = true;
            base._EnterTree();
        }

        public override void _Ready()
        {
            // The smoke fixture supplies in-memory settings and must not persist user settings.
        }
    }
}
