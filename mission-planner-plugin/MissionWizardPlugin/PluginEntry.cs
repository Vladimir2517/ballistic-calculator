using System;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private ToolStripMenuItem menuItem;
        private ToolStripMenuItem setTargetItem;
        private ToolStripMenuItem clearTargetItem;
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

                setTargetItem = new ToolStripMenuItem("Set Delivery Target Here");
                setTargetItem.Click += OnSetTargetClick;

                clearTargetItem = new ToolStripMenuItem("Clear Delivery Target");
                clearTargetItem.Click += OnClearTargetClick;

                // Preferred placement: Flight Planner tab context menu.
                if (Host.FPMenuMap != null)
                {
                    menuOwnerItems = Host.FPMenuMap.Items;
                    menuOwnerItems.Add(setTargetItem);
                    menuOwnerItems.Add(clearTargetItem);
                    menuOwnerItems.Add(menuItem);
                }
                else if (Host.FDMenuMap != null)
                {
                    // Fallback for older builds: Flight Data map context menu.
                    menuOwnerItems = Host.FDMenuMap.Items;
                    menuOwnerItems.Add(setTargetItem);
                    menuOwnerItems.Add(clearTargetItem);
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
                if (setTargetItem != null)
                {
                    setTargetItem.Click -= OnSetTargetClick;
                }
                if (clearTargetItem != null)
                {
                    clearTargetItem.Click -= OnClearTargetClick;
                }

                if (menuOwnerItems != null && menuOwnerItems.Contains(menuItem))
                {
                    menuOwnerItems.Remove(menuItem);
                }
                if (menuOwnerItems != null && setTargetItem != null && menuOwnerItems.Contains(setTargetItem))
                {
                    menuOwnerItems.Remove(setTargetItem);
                }
                if (menuOwnerItems != null && clearTargetItem != null && menuOwnerItems.Contains(clearTargetItem))
                {
                    menuOwnerItems.Remove(clearTargetItem);
                }

                menuItem.Dispose();
                setTargetItem?.Dispose();
                clearTargetItem?.Dispose();
                menuItem = null;
                setTargetItem = null;
                clearTargetItem = null;
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

        private void OnSetTargetClick(object sender, EventArgs e)
        {
            try
            {
                var pointObj = Host.GetType().GetProperty("FPMenuMapPosition")?.GetValue(Host, null);
                if (pointObj == null)
                {
                    throw new InvalidOperationException("Map position is unavailable.");
                }

                var pointType = pointObj.GetType();
                var latObj = pointType.GetProperty("Lat")?.GetValue(pointObj, null);
                var lonObj = pointType.GetProperty("Lng")?.GetValue(pointObj, null)
                    ?? pointType.GetProperty("Lon")?.GetValue(pointObj, null);

                if (latObj == null || lonObj == null)
                {
                    throw new InvalidOperationException("Could not read map position coordinates.");
                }

                var lat = Convert.ToDouble(latObj);
                var lon = Convert.ToDouble(lonObj);

                DeliveryTargetStore.Set(lat, lon);
                MessageBox.Show(
                    $"Delivery target set:\nLat: {lat:F6}\nLon: {lon:F6}",
                    "Mission Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to set target from map:\n" + ex.Message,
                    "Mission Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnClearTargetClick(object sender, EventArgs e)
        {
            DeliveryTargetStore.Clear();
            MessageBox.Show(
                "Delivery target cleared.",
                "Mission Wizard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
