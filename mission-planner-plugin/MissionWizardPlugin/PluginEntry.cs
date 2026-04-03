using System;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private ToolStripMenuItem menuItem;
        private ToolStripItemCollection menuOwnerItems;

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

                // Preferred placement: Flight Planner tab context menu.
                if (Host.FPMenuMap != null)
                {
                    menuOwnerItems = Host.FPMenuMap.Items;
                    menuOwnerItems.Add(menuItem);
                }
                else if (Host.FDMenuMap != null)
                {
                    // Fallback for older builds: Flight Data map context menu.
                    menuOwnerItems = Host.FDMenuMap.Items;
                    menuOwnerItems.Add(menuItem);
                }
                else
                {
                    return false;
                }

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

                if (menuOwnerItems != null && menuOwnerItems.Contains(menuItem))
                {
                    menuOwnerItems.Remove(menuItem);
                }

                menuItem.Dispose();
                menuItem = null;
                menuOwnerItems = null;
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
