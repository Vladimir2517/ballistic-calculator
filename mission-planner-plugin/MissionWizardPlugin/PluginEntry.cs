using System;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private ToolStripMenuItem menuItem;

        public override string Name => "Mission Wizard";
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
                menuItem = new ToolStripMenuItem("Mission Wizard");
                menuItem.Click += OnMenuClick;
                Host.FDMenuMap.Items.Add(menuItem);
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
            if (menuItem != null)
            {
                menuItem.Click -= OnMenuClick;
                menuItem.Dispose();
                menuItem = null;
            }

            return true;
        }

        private void OnMenuClick(object sender, EventArgs e)
        {
            using (var wizard = new MissionWizardForm(Host))
            {
                wizard.ShowDialog();
            }
        }
    }
}
