using System;
using System.Drawing;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal sealed class MissionMapPointController : IDisposable
    {
        private readonly PluginHost host;
        private readonly GMapControl map;
        private readonly GMapOverlay overlay;
        private readonly Button builderButton;

        private bool autoMissionMode;
        private bool pickingMode;
        private int pickStep;

        private static readonly Color ButtonNormalColor = Color.FromArgb(29, 78, 216);
        private static readonly Color ButtonActiveColor = Color.FromArgb(180, 30, 30);

        public MissionMapPointController(PluginHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            map = host.FPGMapControl ?? throw new InvalidOperationException("Карта Flight Planner недоступна.");

            overlay = new GMapOverlay("MissionWizardPoints");
            map.Overlays.Add(overlay);

            builderButton = CreateBuilderButton(map.Parent);
            builderButton.Click += OnBuilderButtonClick;

            map.MouseClick += OnMapMouseClick;
            MissionPointsStore.PointsChanged += OnPointsChanged;

            RefreshMarkers();
        }

        public void Dispose()
        {
            MissionPointsStore.PointsChanged -= OnPointsChanged;
            map.MouseClick -= OnMapMouseClick;

            if (builderButton != null)
            {
                builderButton.Click -= OnBuilderButtonClick;
                var parent = builderButton.Parent;
                if (parent != null && parent.Controls.Contains(builderButton))
                {
                    parent.Controls.Remove(builderButton);
                }
                builderButton.Dispose();
            }

            if (overlay != null)
            {
                overlay.Clear();
                if (map.Overlays.Contains(overlay))
                {
                    map.Overlays.Remove(overlay);
                }
            }
        }

        public void RefreshMarkers()
        {
            overlay.Markers.Clear();

            if (MissionPointsStore.HasStart)
            {
                overlay.Markers.Add(CreateMarker(
                    MissionPointsStore.StartLat,
                    MissionPointsStore.StartLon,
                    "СТАРТ",
                    GMarkerGoogleType.green_dot));
            }

            if (MissionPointsStore.HasDelivery)
            {
                overlay.Markers.Add(CreateMarker(
                    MissionPointsStore.DeliveryLat,
                    MissionPointsStore.DeliveryLon,
                    "ДОСТАВКА",
                    GMarkerGoogleType.red_dot));
            }

            if (MissionPointsStore.HasLanding)
            {
                overlay.Markers.Add(CreateMarker(
                    MissionPointsStore.LandingLat,
                    MissionPointsStore.LandingLon,
                    "ПОСАДКА",
                    GMarkerGoogleType.blue_dot));
            }

            map.Refresh();
        }

        private static Button CreateBuilderButton(Control parent)
        {
            var btn = new Button
            {
                Width = 180,
                Height = 34,
                Text = "Балістика",
                BackColor = Color.FromArgb(29, 78, 216),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = Math.Max(10, parent.Width - 190),
                Top = Math.Max(10, parent.Height - 46)
            };

            btn.FlatAppearance.BorderSize = 0;
            parent.Controls.Add(btn);
            btn.BringToFront();
            return btn;
        }

        private void OnBuilderButtonClick(object sender, EventArgs e)
        {
            // If already in auto mode — cancel
            if (autoMissionMode)
            {
                autoMissionMode = false;
                builderButton.Text = "Балістика";
                builderButton.BackColor = ButtonNormalColor;
                return;
            }

            // If already in picking mode — cancel
            if (pickingMode)
            {
                pickingMode = false;
                pickStep = 0;
                builderButton.Text = "Балістика";
                builderButton.BackColor = ButtonNormalColor;
                return;
            }

            // If all three points set — open wizard
            if (MissionPointsStore.HasStart && MissionPointsStore.HasDelivery && MissionPointsStore.HasLanding)
            {
                QueueOpenWizard();
                return;
            }

            // Default: start single-click auto-mission mode
            autoMissionMode = true;
            builderButton.Text = "▶ Оберіть ціль...";
            builderButton.BackColor = ButtonActiveColor;
        }

        private void OnMapMouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                PointLatLng p;
                try
                {
                    p = map.FromLocalToLatLng(e.X, e.Y);
                }
                catch
                {
                    return;
                }

                // Auto-mission mode: single click builds full mission
                if (autoMissionMode)
                {
                    autoMissionMode = false;
                    builderButton.Text = "Балістика";
                    builderButton.BackColor = ButtonNormalColor;

                    var uiTarget = host?.MainForm as Control;
                    if (uiTarget != null && !uiTarget.IsDisposed)
                    {
                        uiTarget.BeginInvoke(new Action(() =>
                            AutoMissionService.Execute(host, p.Lat, p.Lng)));
                    }
                    else
                    {
                        AutoMissionService.Execute(host, p.Lat, p.Lng);
                    }
                    return;
                }

                // Manual 3-point picking mode (used from context menu)
                if (!pickingMode)
                {
                    return;
                }

                if (pickStep == 0)
                {
                    MissionPointsStore.SetStart(p.Lat, p.Lng);
                    pickStep = 1;
                    return;
                }

                if (pickStep == 1)
                {
                    MissionPointsStore.SetDelivery(p.Lat, p.Lng);
                    pickStep = 2;
                    return;
                }

                MissionPointsStore.SetLanding(p.Lat, p.Lng);
                pickingMode = false;
                pickStep = 0;

                MessageBox.Show(
                    "Точки зібрано: СТАРТ, ДОСТАВКА, ПОСАДКА.\nЗараз відкриється Майстер місії.",
                    "Балістика",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                QueueOpenWizard();
            }
            catch (Exception ex)
            {
                autoMissionMode = false;
                pickingMode = false;
                pickStep = 0;
                builderButton.Text = "Балістика";
                builderButton.BackColor = ButtonNormalColor;

                MessageBox.Show(
                    "Помилка обробки кліку на карті:\n" + ex.Message,
                    "Балістика",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnPointsChanged()
        {
            RefreshMarkers();
        }

        private void QueueOpenWizard()
        {
            var uiTarget = host?.MainForm as Control;
            if (uiTarget != null && !uiTarget.IsDisposed)
            {
                uiTarget.BeginInvoke(new Action(() => WizardDialogService.OpenWizard(host)));
                return;
            }

            WizardDialogService.OpenWizard(host);
        }

        private static GMapMarker CreateMarker(double lat, double lon, string label, GMarkerGoogleType type)
        {
            var marker = new GMarkerGoogle(new PointLatLng(lat, lon), type)
            {
                ToolTipText = label,
                ToolTipMode = MarkerTooltipMode.Always
            };
            return marker;
        }
    }
}
