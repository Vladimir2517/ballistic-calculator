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

        private bool pickingMode;
        private int pickStep;

        public MissionMapPointController(PluginHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            map = host.FPGMapControl ?? throw new InvalidOperationException("Flight Planner map is not available.");

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
                    "START",
                    GMarkerGoogleType.green_dot));
            }

            if (MissionPointsStore.HasDelivery)
            {
                overlay.Markers.Add(CreateMarker(
                    MissionPointsStore.DeliveryLat,
                    MissionPointsStore.DeliveryLon,
                    "DELIVERY",
                    GMarkerGoogleType.red_dot));
            }

            if (MissionPointsStore.HasLanding)
            {
                overlay.Markers.Add(CreateMarker(
                    MissionPointsStore.LandingLat,
                    MissionPointsStore.LandingLon,
                    "LANDING",
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
                Text = "Mission Builder",
                BackColor = Color.FromArgb(29, 78, 216),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Left = Math.Max(10, parent.Width - 190),
                Top = 12
            };

            btn.FlatAppearance.BorderSize = 0;
            parent.Controls.Add(btn);
            btn.BringToFront();
            return btn;
        }

        private void OnBuilderButtonClick(object sender, EventArgs e)
        {
            // Open Flight Planner tab context.
            var menuFlightPlannerField = host.MainForm?.GetType().GetField("MenuFlightPlanner");
            var menuFlightPlanner = menuFlightPlannerField?.GetValue(host.MainForm) as ToolStripButton;
            menuFlightPlanner?.PerformClick();

            pickingMode = true;
            pickStep = MissionPointsStore.HasStart
                ? (MissionPointsStore.HasDelivery ? (MissionPointsStore.HasLanding ? 0 : 2) : 1)
                : 0;

            MessageBox.Show(
                "Map mission builder is active.\n\n" +
                "Left click map to set points in order:\n" +
                "1) START\n2) DELIVERY\n3) LANDING\n\n" +
                "After that open Mission Wizard and click Generate Mission.",
                "Mission Builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMapMouseClick(object sender, MouseEventArgs e)
        {
            if (!pickingMode || e.Button != MouseButtons.Left)
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
                "Points collected: START, DELIVERY, LANDING.\nOpen Mission Wizard to generate mission.",
                "Mission Builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnPointsChanged()
        {
            RefreshMarkers();
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
