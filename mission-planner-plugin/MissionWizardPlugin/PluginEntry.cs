using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private MissionMapPointController mapController;

        public override string Name => "Майстер місії";
        public override string Version => "0.1.0";
        public override string Author => "Vladimir2517";

        public override bool Init()
        {
            return true;
        }

        public override bool Loaded()
        {
            try
            {
                mapController = new MissionMapPointController(Host);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Loop()
        {
            return true;
        }

        public override bool Exit()
        {
            if (mapController != null)
            {
                mapController.Dispose();
                mapController = null;
            }

            return true;
        }
    }
}
