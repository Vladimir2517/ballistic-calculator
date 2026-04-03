using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
        private readonly Label windLabel;
        private readonly Timer windTimer;

        private bool autoMissionMode;
        private bool pickingMode;
        private int pickStep;

        private static readonly Color ButtonNormalColor = Color.FromArgb(29, 78, 216);
        private static readonly Color ButtonActiveColor = Color.FromArgb(180, 30, 30);

        private static readonly Color WindColorAutopilot  = Color.FromArgb(0,  200, 100);
        private static readonly Color WindColorForecast   = Color.FromArgb(30, 144, 255);
        private static readonly Color WindColorXlsx       = Color.FromArgb(255, 165,   0);
        private static readonly Color WindColorNoData     = Color.FromArgb(140, 140, 140);

        // Forecast cache — updated in background every 2 minutes
        private float _forecastDir;
        private float _forecastSpeed;
        private bool  _forecastValid;
        private DateTime _lastForecastFetch = DateTime.MinValue;
        private volatile bool _forecastFetching;

        public MissionMapPointController(PluginHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            map = host.FPGMapControl ?? throw new InvalidOperationException("Карта Flight Planner недоступна.");

            overlay = new GMapOverlay("MissionWizardPoints");
            map.Overlays.Add(overlay);

            builderButton = CreateBuilderButton(map.Parent);
            builderButton.Click += OnBuilderButtonClick;

            windLabel = CreateWindLabel(map.Parent, builderButton);

            windTimer = new Timer { Interval = 3000 };
            windTimer.Tick += (_, __) => UpdateWindIndicator();
            windTimer.Start();

            map.MouseClick += OnMapMouseClick;
            MissionPointsStore.PointsChanged += OnPointsChanged;

            RefreshMarkers();
            UpdateWindIndicator();
        }

        public void Dispose()
        {
            windTimer?.Stop();
            windTimer?.Dispose();

            MissionPointsStore.PointsChanged -= OnPointsChanged;
            map.MouseClick -= OnMapMouseClick;

            if (windLabel != null)
            {
                var parent = windLabel.Parent;
                if (parent != null && parent.Controls.Contains(windLabel))
                    parent.Controls.Remove(windLabel);
                windLabel.Dispose();
            }

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
            overlay.Routes.Clear();

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

            AddWindVisualization();

            map.Refresh();
        }

        private void AddWindVisualization()
        {
            TryGetCurrentWind(out var windFromDeg, out var windSpeedMps, out _);
            if (windSpeedMps < 0.1f)
            {
                return;
            }

            var anchor = ResolveWindAnchorPoint();
            var hasAutopilot = TryReadWindFromAutopilot(out var apDir, out var apSpeed) && apSpeed > 0.1f;

            if (hasAutopilot)
                DrawWindArrow(anchor, apDir, apSpeed,
                    "АВТОПІЛОТ", WindColorAutopilot,
                    GMarkerGoogleType.green_dot, GMarkerGoogleType.green_small);

            if (_forecastValid && _forecastSpeed > 0.1f)
            {
                PointLatLng forecastAnchor;
                if (hasAutopilot)
                {
                    var off = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, (_forecastDir + 90.0) % 360.0, 250.0);
                    forecastAnchor = new PointLatLng(off.lat, off.lon);
                }
                else
                {
                    forecastAnchor = anchor;
                }
                DrawWindArrow(forecastAnchor, _forecastDir, _forecastSpeed,
                    "ПРОГНОЗ", WindColorForecast,
                    GMarkerGoogleType.lightblue_dot, GMarkerGoogleType.blue_small);
            }


        private void DrawWindArrow(
            PointLatLng anchor, float fromDeg, float speedMps,
            string label, Color color,
            GMarkerGoogleType tailType, GMarkerGoogleType headType)
        {
            var arrowLen = Math.Min(1400.0, 350.0 + speedMps * 50.0);
            var tail = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, fromDeg, arrowLen);
            var head = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, (fromDeg + 180.0) % 360.0, arrowLen);

            overlay.Routes.Add(new GMapRoute(new List<PointLatLng>
            {
                new PointLatLng(tail.lat, tail.lon),
                new PointLatLng(head.lat, head.lon)
            }, "Wind_" + label) { Stroke = new Pen(color, 2.5f) });

            overlay.Markers.Add(CreateMarker(tail.lat, tail.lon,
                string.Format(CultureInfo.InvariantCulture,
                    "{0}: з {1:F0}° {2:F1} м/с", label, fromDeg, speedMps), tailType));
            overlay.Markers.Add(CreateMarker(head.lat, head.lon,
                WindArrow((fromDeg + 180.0f) % 360.0f) + " " + label, headType));
        }
            var anchor = ResolveWindAnchorPoint();
            var fromPoint = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, windFromDeg, 500.0);
            var toPoint   = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, (windFromDeg + 180.0) % 360.0, 500.0);

            var route = new GMapRoute(new List<PointLatLng>
            {
                new PointLatLng(fromPoint.lat, fromPoint.lon),
                new PointLatLng(toPoint.lat,   toPoint.lon)
            }, "WindDirection")
            {
                Stroke = new Pen(Color.DeepSkyBlue, 2.5f)
            };
            overlay.Routes.Add(route);

            overlay.Markers.Add(CreateMarker(
                fromPoint.lat, fromPoint.lon,
                string.Format(CultureInfo.InvariantCulture,
                    "ВІТЕР З {0:F0}° ({1:F1} м/с)", windFromDeg, windSpeedMps),
                GMarkerGoogleType.lightblue_dot));

            overlay.Markers.Add(CreateMarker(
                toPoint.lat, toPoint.lon,
                "↓ КУДИ ДУЄ ВІТЕР",
                GMarkerGoogleType.blue_small));
        }
            private void AddWindVisualization()
            {
                var anchor = ResolveWindAnchorPoint();
                var hasAutopilot = TryReadWindFromAutopilot(out var apDir, out var apSpeed) && apSpeed > 0.1f;

                if (hasAutopilot)
                    DrawWindArrow(anchor, apDir, apSpeed,
                        "АВТОПІЛОТ", WindColorAutopilot,
                        GMarkerGoogleType.green_dot, GMarkerGoogleType.green_small);

                if (_forecastValid && _forecastSpeed > 0.1f)
                {
                    PointLatLng forecastAnchor;
                    if (hasAutopilot)
                    {
                        var off = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, (_forecastDir + 90.0) % 360.0, 250.0);
                        forecastAnchor = new PointLatLng(off.lat, off.lon);
                    }
                    else
                    {
                        forecastAnchor = anchor;
                    }
                    DrawWindArrow(forecastAnchor, _forecastDir, _forecastSpeed,
                        "ПРОГНОЗ", WindColorForecast,
                        GMarkerGoogleType.lightblue_dot, GMarkerGoogleType.blue_small);
                }
            }

            private void DrawWindArrow(
                PointLatLng anchor, float fromDeg, float speedMps,
                string label, Color color,
                GMarkerGoogleType tailType, GMarkerGoogleType headType)
            {
                var arrowLen = Math.Min(1400.0, 350.0 + speedMps * 50.0);
                var tail = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, fromDeg, arrowLen);
                var head = OffsetLatLonByBearing(anchor.Lat, anchor.Lng, (fromDeg + 180.0) % 360.0, arrowLen);

                overlay.Routes.Add(new GMapRoute(new List<PointLatLng>
                {
                    new PointLatLng(tail.lat, tail.lon),
                    new PointLatLng(head.lat, head.lon)
                }, "Wind_" + label) { Stroke = new Pen(color, 2.5f) });

                overlay.Markers.Add(CreateMarker(tail.lat, tail.lon,
                    string.Format(CultureInfo.InvariantCulture,
                        "{0}: з {1:F0}° {2:F1} м/с", label, fromDeg, speedMps), tailType));
                overlay.Markers.Add(CreateMarker(head.lat, head.lon,
                    WindArrow((fromDeg + 180.0f) % 360.0f) + " " + label, headType));
            }

        private void UpdateWindIndicator()
        {
              ScheduleForecastUpdate();

              if (windLabel == null || windLabel.IsDisposed) return;

            TryGetCurrentWind(out var dir, out var speed, out var source);

            string text;
            Color foreColor;

            if (speed < 0.1f)
            {
                text      = "Вітер: немає даних";
                foreColor = WindColorNoData;
            }
            else
            {
                var arrow = WindArrow(dir);
                text = string.Format(CultureInfo.InvariantCulture,
                    "{0} {1:F1} м/с з {2:F0}°  [{3}]",
                    arrow, speed, dir, source);

                foreColor = source == "Автопілот" ? WindColorAutopilot
                          : source == "XLSX"       ? WindColorXlsx
                          :                          WindColorForecast;
            }

            Action update = () =>
            {
                if (windLabel.IsDisposed) return;
                windLabel.Text      = text;
                windLabel.ForeColor = foreColor;
                windLabel.Width     = Math.Max(180, TextRenderer.MeasureText(text, windLabel.Font).Width + 16);
                windLabel.Left      = Math.Max(4, windLabel.Parent.Width - windLabel.Width - 4);

                // Also refresh arrow on map if wind changed
                overlay.Routes.Clear();
                var markers = new List<GMapMarker>(overlay.Markers);
                foreach (var m in markers)
                    if (m.ToolTipText != null &&
                            (m.ToolTipText.StartsWith("АВТОПІЛОТ") || m.ToolTipText.StartsWith("ПРОГНОЗ") ||
                             m.ToolTipText.StartsWith("ВІТЕР") || m.ToolTipText.StartsWith("↓") ||
                             m.ToolTipText.StartsWith("↑") || m.ToolTipText.StartsWith("→") ||
                             m.ToolTipText.StartsWith("←") || m.ToolTipText.StartsWith("↗") ||
                             m.ToolTipText.StartsWith("↘") || m.ToolTipText.StartsWith("↙") ||
                             m.ToolTipText.StartsWith("↖")))
                        overlay.Markers.Remove(m);
                AddWindVisualization();
                map.Refresh();
            };

            try
            {
                if (windLabel.InvokeRequired)
                    windLabel.BeginInvoke(update);
                else
                    update();
            }
            catch { /* plugin unloading */ }
        }

        private void TryGetCurrentWind(out float dir, out float speed, out string source)
        {
            dir = 0; speed = 0; source = "Немає даних";
            if (TryReadWindFromAutopilot(out dir, out speed) && speed > 0.1f)
            {
                source = "Автопілот";
                return;
            }
            // Use cached forecast — never block UI thread
            if (_forecastValid && _forecastSpeed > 0.1f)
            {
                dir = _forecastDir; speed = _forecastSpeed; source = "Open-Meteo";
            }
        }

        // Fires a background Task to refresh forecast cache (at most once per 2 min).
        private void ScheduleForecastUpdate()
        {
            if (_forecastFetching) return;
            if ((DateTime.UtcNow - _lastForecastFetch).TotalSeconds < 120) return;
            if (!MissionPointsStore.HasDelivery && !MissionPointsStore.HasStart) return;

            _forecastFetching = true;
            var anchor = ResolveWindAnchorPoint();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (TryReadWindFromForecastSync(anchor.Lat, anchor.Lng, out var d, out var s))
                    {
                        _forecastDir   = d;
                        _forecastSpeed = s;
                        _forecastValid = true;
                        _lastForecastFetch = DateTime.UtcNow;
                    }
                }
                finally { _forecastFetching = false; }
            });
        }

        private static bool TryReadWindFromForecastSync(double lat, double lon, out float dir, out float speed)
        {
            dir = 0; speed = 0;
            try
            {
                var url = string.Format(CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=wind_speed_10m,wind_direction_10m&wind_speed_unit=ms",
                    lat, lon);
                var req = System.Net.WebRequest.Create(url);
                req.Timeout = 1200;
                using (var resp = req.GetResponse())
                using (var sr   = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    var json = sr.ReadToEnd();
                    var sm = System.Text.RegularExpressions.Regex.Match(
                        json, @"""wind_speed_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    var dm = System.Text.RegularExpressions.Regex.Match(
                        json, @"""wind_direction_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    if (!sm.Success || !dm.Success) return false;
                    speed = float.Parse(sm.Groups["v"].Value, CultureInfo.InvariantCulture);
                    dir   = float.Parse(dm.Groups["v"].Value, CultureInfo.InvariantCulture);
                    return speed > 0.1f;
                }
            }
            catch { return false; }
        }

        private static string WindArrow(float fromDeg)
        {
            // Arrow points the direction wind is BLOWING TOWARD
            var toward = (fromDeg + 180.0) % 360.0;
            var idx = (int)Math.Round(toward / 45.0) % 8;
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return arrows[idx];
        }

        private PointLatLng ResolveWindAnchorPoint()
        {
            if (MissionPointsStore.HasDelivery)
            {
                return new PointLatLng(MissionPointsStore.DeliveryLat, MissionPointsStore.DeliveryLon);
            }

            if (MissionPointsStore.HasStart)
            {
                return new PointLatLng(MissionPointsStore.StartLat, MissionPointsStore.StartLon);
            }

            return map.Position;
        }

        private static Label CreateWindLabel(Control parent, Button anchorButton)
        {
            var lbl = new Label
            {
                AutoSize  = false,
                Width     = 200,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(200, 15, 15, 15),
                ForeColor = WindColorNoData,
                Font      = new Font("Consolas", 9f, FontStyle.Bold),
                Text      = "Вітер: ...",
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
                Left      = Math.Max(4, parent.Width - 204),
                Top       = anchorButton.Top - 26
            };
            parent.Controls.Add(lbl);
            lbl.BringToFront();
            return lbl;
        }

        private bool TryReadWindFromAutopilot(out float dir, out float speed)
        {
            dir = 0;
            speed = 0;

            try
            {
                var cs = host?.GetType().GetProperty("cs")?.GetValue(host, null);
                if (cs == null)
                {
                    return false;
                }

                var dirObj = cs.GetType().GetProperty("wind_dir")?.GetValue(cs, null);
                var speedObj = cs.GetType().GetProperty("wind_vel")?.GetValue(cs, null);
                if (dirObj == null || speedObj == null)
                {
                    return false;
                }

                dir = Convert.ToSingle(dirObj, CultureInfo.InvariantCulture);
                speed = Convert.ToSingle(speedObj, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static (double lat, double lon) OffsetLatLonByBearing(
            double startLat,
            double startLon,
            double bearingDeg,
            double distanceMeters)
        {
            const double earthRadiusMeters = 6378137.0;
            var bearing = bearingDeg * Math.PI / 180.0;
            var north = Math.Cos(bearing) * distanceMeters;
            var east = Math.Sin(bearing) * distanceMeters;

            var dLat = north / earthRadiusMeters;
            var dLon = east / (earthRadiusMeters * Math.Cos(startLat * Math.PI / 180.0));
            var latOut = startLat + (dLat * 180.0 / Math.PI);
            var lonOut = startLon + (dLon * 180.0 / Math.PI);
            return (latOut, lonOut);
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
                    ScheduleForecastUpdate();

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
                                (m.ToolTipText.StartsWith("АВТОПІЛОТ") || m.ToolTipText.StartsWith("ПРОГНОЗ") ||
                                 m.ToolTipText.StartsWith("ВІТЕР") || m.ToolTipText.StartsWith("↓") ||
                                 m.ToolTipText.StartsWith("↑") || m.ToolTipText.StartsWith("→") ||
                                 m.ToolTipText.StartsWith("←") || m.ToolTipText.StartsWith("↗") ||
                                 m.ToolTipText.StartsWith("↘") || m.ToolTipText.StartsWith("↙") ||
                                 m.ToolTipText.StartsWith("↖")))
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
