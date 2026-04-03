using System;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private ToolStripMenuItem menuItem;
        private ToolStripMenuItem setStartItem;
        private ToolStripMenuItem setDeliveryItem;
        private ToolStripMenuItem setLandingItem;
        private ToolStripMenuItem clearPointsItem;
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

                setStartItem = new ToolStripMenuItem("Set Start Point Here");
                setStartItem.Click += OnSetStartClick;

                setDeliveryItem = new ToolStripMenuItem("Set Delivery Point Here");
                setDeliveryItem.Click += OnSetDeliveryClick;

                setLandingItem = new ToolStripMenuItem("Set Landing Point Here");
                setLandingItem.Click += OnSetLandingClick;

                clearPointsItem = new ToolStripMenuItem("Clear Mission Points");
                clearPointsItem.Click += OnClearPointsClick;

                // Preferred placement: Flight Planner tab context menu.
                if (Host.FPMenuMap != null)
                {
                    menuOwnerItems = Host.FPMenuMap.Items;
                    menuOwnerItems.Add(setStartItem);
                    menuOwnerItems.Add(setDeliveryItem);
                    menuOwnerItems.Add(setLandingItem);
                    menuOwnerItems.Add(clearPointsItem);
                    menuOwnerItems.Add(menuItem);
                }
                else if (Host.FDMenuMap != null)
                {
                    // Fallback for older builds: Flight Data map context menu.
                    menuOwnerItems = Host.FDMenuMap.Items;
                    menuOwnerItems.Add(setStartItem);
                    menuOwnerItems.Add(setDeliveryItem);
                    menuOwnerItems.Add(setLandingItem);
                    menuOwnerItems.Add(clearPointsItem);
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
                if (setStartItem != null)
                {
                    setStartItem.Click -= OnSetStartClick;
                }
                if (setDeliveryItem != null)
                {
                    setDeliveryItem.Click -= OnSetDeliveryClick;
                }
                if (setLandingItem != null)
                {
                    setLandingItem.Click -= OnSetLandingClick;
                }
                if (clearPointsItem != null)
                {
                    clearPointsItem.Click -= OnClearPointsClick;
                }

                if (menuOwnerItems != null && menuOwnerItems.Contains(menuItem))
                {
                    menuOwnerItems.Remove(menuItem);
                }
                if (menuOwnerItems != null && setStartItem != null && menuOwnerItems.Contains(setStartItem))
                {
                    menuOwnerItems.Remove(setStartItem);
                }
                if (menuOwnerItems != null && setDeliveryItem != null && menuOwnerItems.Contains(setDeliveryItem))
                {
                    menuOwnerItems.Remove(setDeliveryItem);
                }
                if (menuOwnerItems != null && setLandingItem != null && menuOwnerItems.Contains(setLandingItem))
                {
                    menuOwnerItems.Remove(setLandingItem);
                }
                if (menuOwnerItems != null && clearPointsItem != null && menuOwnerItems.Contains(clearPointsItem))
                {
                    menuOwnerItems.Remove(clearPointsItem);
                }

                menuItem.Dispose();
                setStartItem?.Dispose();
                setDeliveryItem?.Dispose();
                setLandingItem?.Dispose();
                clearPointsItem?.Dispose();
                menuItem = null;
                setStartItem = null;
                setDeliveryItem = null;
                setLandingItem = null;
                clearPointsItem = null;
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

        private bool TryGetMenuLatLon(out double lat, out double lon)
        {
            lat = 0;
            lon = 0;

            try
            {
                var pointObj = Host.GetType().GetProperty("FPMenuMapPosition")?.GetValue(Host, null);
                if (pointObj == null)
                {
                    return false;
                }

                var pointType = pointObj.GetType();
                var latObj = pointType.GetProperty("Lat")?.GetValue(pointObj, null);
                var lonObj = pointType.GetProperty("Lng")?.GetValue(pointObj, null)
                    ?? pointType.GetProperty("Lon")?.GetValue(pointObj, null);

                if (latObj == null || lonObj == null)
                {
                    return false;
                }

                lat = Convert.ToDouble(latObj);
                lon = Convert.ToDouble(lonObj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnSetStartClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Failed to read map coordinates.",
                    "Mission Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetStart(lat, lon);
            MessageBox.Show($"Start point set:\nLat: {lat:F6}\nLon: {lon:F6}",
                "Mission Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSetDeliveryClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Failed to read map coordinates.",
                    "Mission Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetDelivery(lat, lon);
            MessageBox.Show($"Delivery point set:\nLat: {lat:F6}\nLon: {lon:F6}",
                "Mission Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSetLandingClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Failed to read map coordinates.",
                    "Mission Wizard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetLanding(lat, lon);
            MessageBox.Show($"Landing point set:\nLat: {lat:F6}\nLon: {lon:F6}",
                "Mission Wizard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnClearPointsClick(object sender, EventArgs e)
        {
            MissionPointsStore.ClearAll();
            MessageBox.Show(
                "Mission points cleared.",
                "Mission Wizard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
