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
        private ToolStripMenuItem autoMissionItem;
        private ToolStripItemCollection menuOwnerItems;
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
                menuItem = new ToolStripMenuItem("Майстер місії");
                menuItem.Click += OnMenuClick;

                setStartItem = new ToolStripMenuItem("Встановити точку старту тут");
                setStartItem.Click += OnSetStartClick;

                setDeliveryItem = new ToolStripMenuItem("Встановити точку доставки тут");
                setDeliveryItem.Click += OnSetDeliveryClick;

                setLandingItem = new ToolStripMenuItem("Встановити точку посадки тут");
                setLandingItem.Click += OnSetLandingClick;

                clearPointsItem = new ToolStripMenuItem("Очистити точки місії");
                clearPointsItem.Click += OnClearPointsClick;

                autoMissionItem = new ToolStripMenuItem("⚡ Бомбометання сюди (авто)");
                autoMissionItem.Click += OnAutoMissionClick;

                // Preferred placement: Flight Planner tab context menu.
                if (Host.FPMenuMap != null)
                {
                    menuOwnerItems = Host.FPMenuMap.Items;
                    menuOwnerItems.Add(autoMissionItem);
                    menuOwnerItems.Add(setStartItem);
                    menuOwnerItems.Add(setDeliveryItem);
                    menuOwnerItems.Add(setLandingItem);
                    menuOwnerItems.Add(clearPointsItem);
                    menuOwnerItems.Add(menuItem);
                }
                else if (Host.FDMenuMap != null)
                {
                    // Запасний варіант для старіших збірок: контекстне меню карти Flight Data.
                    menuOwnerItems = Host.FDMenuMap.Items;
                    menuOwnerItems.Add(autoMissionItem);
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
                if (menuOwnerItems != null && autoMissionItem != null && menuOwnerItems.Contains(autoMissionItem))
                {
                    menuOwnerItems.Remove(autoMissionItem);
                }

                menuItem.Dispose();
                setStartItem?.Dispose();
                setDeliveryItem?.Dispose();
                setLandingItem?.Dispose();
                clearPointsItem?.Dispose();
                autoMissionItem?.Dispose();
                menuItem = null;
                setStartItem = null;
                setDeliveryItem = null;
                setLandingItem = null;
                clearPointsItem = null;
                menuOwnerItems = null;
            }

            if (mapController != null)
            {
                mapController.Dispose();
                mapController = null;
            }

            return true;
        }

        private void OnMenuClick(object sender, EventArgs e)
        {
            WizardDialogService.OpenWizard(Host);
        }

        private void OnAutoMissionClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Не вдалося прочитати координати з карти.",
                    "Автоматична місія",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            AutoMissionService.Execute(Host, lat, lon);
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
                    "Не вдалося прочитати координати з карти.",
                    "Майстер місії",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetStart(lat, lon);
            mapController?.RefreshMarkers();
            MessageBox.Show($"Точку старту встановлено:\nШирота: {lat:F6}\nДовгота: {lon:F6}",
                "Майстер місії", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSetDeliveryClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Не вдалося прочитати координати з карти.",
                    "Майстер місії",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetDelivery(lat, lon);
            mapController?.RefreshMarkers();
            MessageBox.Show($"Точку доставки встановлено:\nШирота: {lat:F6}\nДовгота: {lon:F6}",
                "Майстер місії", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSetLandingClick(object sender, EventArgs e)
        {
            if (!TryGetMenuLatLon(out var lat, out var lon))
            {
                MessageBox.Show(
                    "Не вдалося прочитати координати з карти.",
                    "Майстер місії",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MissionPointsStore.SetLanding(lat, lon);
            mapController?.RefreshMarkers();
            MessageBox.Show($"Точку посадки встановлено:\nШирота: {lat:F6}\nДовгота: {lon:F6}",
                "Майстер місії", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnClearPointsClick(object sender, EventArgs e)
        {
            MissionPointsStore.ClearAll();
            mapController?.RefreshMarkers();
            MessageBox.Show(
                "Точки місії очищено.",
                "Майстер місії",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
