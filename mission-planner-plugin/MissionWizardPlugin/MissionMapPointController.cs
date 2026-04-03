using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        private static readonly Color WindColorAutopilot = Color.FromArgb(0, 200, 100);
        private static readonly Color WindColorForecast  = Color.FromArgb(30, 144, 255);
        private static readonly Color WindColorXlsx      = Color.FromArgb(255, 165, 0);
        private static readonly Color WindColorNoData    = Color.FromArgb(140, 140, 140);

        // Forecast cache — updated in background every 2 minutes
        private float _forecastDir;
        private float _forecastSpeed;
        private bool _forecastValid;
        private string _forecastSource = "Open-Meteo";
        private string _forecastLastError = "";
        private DateTime _lastForecastFetch = DateTime.MinValue;
        private volatile bool _forecastFetching;
        private readonly string _windyApiKey;

        private bool _hasAutoDropPreview;
        private double _previewTargetLat;
        private double _previewTargetLon;
        private double _previewReleaseLat;
        private double _previewReleaseLon;
        private float _previewReleaseOffsetM;
        private float _previewWindDir;
        private float _previewWindSpeed;
        private string _previewWindSource = "Немає даних";

        private static readonly string[] XlsxSearchPaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "Таблица_бомбометания.xlsx"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Таблица_бомбометания.xlsx"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MissionWizard", "Таблица_бомбометания.xlsx"),
            @"C:\Projects\ballistic-calculator\input\Таблица_бомбометания.xlsx"
        };


        public MissionMapPointController(PluginHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            map = host.FPGMapControl ?? throw new InvalidOperationException("Карта Flight Planner недоступна.");
            _windyApiKey = (Environment.GetEnvironmentVariable("MISSION_WINDY_API_KEY") ?? string.Empty).Trim();

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
                    parent.Controls.Remove(builderButton);
                builderButton.Dispose();
            }

            if (overlay != null)
            {
                overlay.Clear();
                if (map.Overlays.Contains(overlay))
                    map.Overlays.Remove(overlay);
            }
        }

        public void RefreshMarkers()
        {
            overlay.Markers.Clear();
            overlay.Routes.Clear();

            if (MissionPointsStore.HasStart)
                overlay.Markers.Add(CreateMarker(MissionPointsStore.StartLat, MissionPointsStore.StartLon, "СТАРТ", GMarkerGoogleType.green_dot));

            if (MissionPointsStore.HasDelivery)
                overlay.Markers.Add(CreateMarker(MissionPointsStore.DeliveryLat, MissionPointsStore.DeliveryLon, "ДОСТАВКА", GMarkerGoogleType.red_dot));

            if (MissionPointsStore.HasLanding)
                overlay.Markers.Add(CreateMarker(MissionPointsStore.LandingLat, MissionPointsStore.LandingLon, "ПОСАДКА", GMarkerGoogleType.blue_dot));

            AddWindVisualization();
            AddAutoDropPreviewVisualization();
            map.Refresh();
        }

        private void AddAutoDropPreviewVisualization()
        {
            if (!_hasAutoDropPreview)
                return;

            overlay.Markers.Add(CreateMarker(
                _previewTargetLat,
                _previewTargetLon,
                "ЦІЛЬ",
                GMarkerGoogleType.red_dot));

            var releaseLabel = string.Format(
                CultureInfo.InvariantCulture,
                "СКИД: {0:F0} м, вітер {1:F1} м/с з {2:F0}° [{3}]",
                _previewReleaseOffsetM,
                _previewWindSpeed,
                _previewWindDir,
                _previewWindSource);

            overlay.Markers.Add(CreateMarker(
                _previewReleaseLat,
                _previewReleaseLon,
                releaseLabel,
                GMarkerGoogleType.orange_dot));

            overlay.Routes.Add(new GMapRoute(new List<PointLatLng>
            {
                new PointLatLng(_previewReleaseLat, _previewReleaseLon),
                new PointLatLng(_previewTargetLat, _previewTargetLon)
            }, "DropPreview") { Stroke = new Pen(Color.FromArgb(230, 126, 34), 2f) });
        }

        private void AddWindVisualization()
        {
            var anchor = ResolveWindAnchorPoint();
            var hasAutopilot = TryReadWindFromAutopilot(out var apDir, out var apSpeed) && apSpeed > 0.1f;

            if (hasAutopilot)
            {
                DrawWindArrow(anchor, apDir, apSpeed, "АВТОПІЛОТ", WindColorAutopilot, GMarkerGoogleType.green_dot, GMarkerGoogleType.green_small);
            }

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
                DrawWindArrow(forecastAnchor, _forecastDir, _forecastSpeed, "ПРОГНОЗ", WindColorForecast, GMarkerGoogleType.lightblue_dot, GMarkerGoogleType.blue_small);
            }
        }

        private void DrawWindArrow(PointLatLng anchor, float fromDeg, float speedMps, string label, Color color, GMarkerGoogleType tailType, GMarkerGoogleType headType)
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
                string.Format(CultureInfo.InvariantCulture, "{0}: з {1:F0}° {2:F1} м/с", label, fromDeg, speedMps), tailType));
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
                if (_forecastFetching)
                    text = "Вітер: оновлення...";
                else if (!string.IsNullOrWhiteSpace(_forecastLastError))
                    text = "Вітер: немає даних (" + _forecastLastError + ")";
                else
                    text = "Вітер: немає даних";
                foreColor = WindColorNoData;
            }
            else
            {
                text = string.Format(CultureInfo.InvariantCulture, "{0} {1:F1} м/с з {2:F0}°  [{3}]", WindArrow(dir), speed, dir, source);
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
                    AddAutoDropPreviewVisualization();
                map.Refresh();
            };

            try
            {
                if (windLabel.InvokeRequired)
                    windLabel.BeginInvoke(update);
                else
                    update();
            }
            catch { }
        }

        private void TryGetCurrentWind(out float dir, out float speed, out string source)
        {
            dir = 0; speed = 0; source = "Немає даних";
            if (TryReadWindFromAutopilot(out dir, out speed) && speed > 0.1f) { source = "Автопілот"; return; }
            if (_forecastValid && _forecastSpeed > 0.1f) { dir = _forecastDir; speed = _forecastSpeed; source = _forecastSource; }
        }

        private void ScheduleForecastUpdate()
        {
            if (_forecastFetching) return;
            var retrySeconds = _forecastValid ? 120 : 15;
            if ((DateTime.UtcNow - _lastForecastFetch).TotalSeconds < retrySeconds) return;

            _forecastFetching = true;
            var anchor = ResolveWindAnchorPoint();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    float d, s;
                    string err;
                    if (TryReadWindFromWindySync(anchor.Lat, anchor.Lng, out d, out s, out err))
                    {
                        _forecastDir = d; _forecastSpeed = s; _forecastValid = true;
                        _forecastSource = "Windy";
                        _forecastLastError = "";
                    }
                    else if (TryReadWindFromForecastSync(anchor.Lat, anchor.Lng, out d, out s, out err))
                    {
                        _forecastDir = d; _forecastSpeed = s; _forecastValid = true;
                        _forecastSource = "Open-Meteo";
                        _forecastLastError = "";
                    }
                    else
                    {
                        _forecastLastError = string.IsNullOrWhiteSpace(err) ? "network" : err;
                    }
                }
                finally
                {
                    _lastForecastFetch = DateTime.UtcNow;
                    _forecastFetching = false;
                }
            });
        }

        private bool TryReadWindFromWindySync(double lat, double lon, out float dir, out float speed, out string error)
        {
            dir = 0; speed = 0;
            error = "";
            if (string.IsNullOrWhiteSpace(_windyApiKey))
            {
                error = "Windy key missing";
                return false;
            }

            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://api.windy.com/api/point-forecast/v2");
                req.Method = "POST";
                req.Timeout = 5000;
                req.ContentType = "application/json";
                req.UserAgent = "MissionWizardPlugin/1.0";

                var body = string.Format(CultureInfo.InvariantCulture,
                    "{{\"lat\":{0},\"lon\":{1},\"model\":\"gfs\",\"parameters\":[\"wind\"],\"levels\":[\"surface\"],\"key\":\"{2}\"}}",
                    lat, lon, _windyApiKey.Replace("\\", "\\\\").Replace("\"", "\\\""));
                var bytes = Encoding.UTF8.GetBytes(body);
                req.ContentLength = bytes.Length;

                using (var rs = req.GetRequestStream())
                    rs.Write(bytes, 0, bytes.Length);

                using (var resp = req.GetResponse())
                using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    var json = sr.ReadToEnd();
                    var um = System.Text.RegularExpressions.Regex.Match(json, @"""wind_u-surface""\s*:\s*\[(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    var vm = System.Text.RegularExpressions.Regex.Match(json, @"""wind_v-surface""\s*:\s*\[(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    if (!um.Success || !vm.Success)
                    {
                        error = "Windy parse";
                        return false;
                    }

                    var u = float.Parse(um.Groups["v"].Value, CultureInfo.InvariantCulture);
                    var v = float.Parse(vm.Groups["v"].Value, CultureInfo.InvariantCulture);

                    speed = (float)Math.Sqrt(u * u + v * v);
                    var towardDeg = Math.Atan2(u, v) * 180.0 / Math.PI;
                    if (towardDeg < 0) towardDeg += 360.0;
                    dir = (float)((towardDeg + 180.0) % 360.0);

                    return speed > 0.1f;
                }
            }
            catch (System.Net.WebException wex)
            {
                var code = (wex.Response as System.Net.HttpWebResponse)?.StatusCode;
                error = code.HasValue ? ("Windy " + (int)code.Value) : "Windy net";
                return false;
            }
            catch
            {
                error = "Windy fail";
                return false;
            }
        }

        private static bool TryReadWindFromForecastSync(double lat, double lon, out float dir, out float speed, out string error)
        {
            dir = 0; speed = 0;
            error = "";
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                var url = string.Format(CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=wind_speed_10m,wind_direction_10m&wind_speed_unit=ms", lat, lon);
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Timeout = 5000;
                req.UserAgent = "MissionWizardPlugin/1.0";
                using (var resp = req.GetResponse())
                using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                {
                    var json = sr.ReadToEnd();
                    var sm = System.Text.RegularExpressions.Regex.Match(json, @"""wind_speed_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    var dm = System.Text.RegularExpressions.Regex.Match(json, @"""wind_direction_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    if (!sm.Success || !dm.Success)
                    {
                        error = "OpenMeteo parse";
                        return false;
                    }
                    speed = float.Parse(sm.Groups["v"].Value, CultureInfo.InvariantCulture);
                    dir   = float.Parse(dm.Groups["v"].Value, CultureInfo.InvariantCulture);
                    return speed > 0.1f;
                }
            }
            catch (System.Net.WebException wex)
            {
                var code = (wex.Response as System.Net.HttpWebResponse)?.StatusCode;
                error = code.HasValue ? ("OpenMeteo " + (int)code.Value) : "OpenMeteo net";
                return false;
            }
            catch
            {
                error = "OpenMeteo fail";
                return false;
            }
        }

        private static string WindArrow(float fromDeg)
        {
            var toward = (fromDeg + 180.0) % 360.0;
            var idx = (int)Math.Round(toward / 45.0) % 8;
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return arrows[idx];
        }

        private PointLatLng ResolveWindAnchorPoint()
        {
            if (MissionPointsStore.HasDelivery) return new PointLatLng(MissionPointsStore.DeliveryLat, MissionPointsStore.DeliveryLon);
            if (MissionPointsStore.HasStart)    return new PointLatLng(MissionPointsStore.StartLat, MissionPointsStore.StartLon);
            return map.Position;
        }

        private static Label CreateWindLabel(Control parent, Button anchorButton)
        {
            var lbl = new Label
            {
                AutoSize  = false, Width = 200, Height = 22,
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
            dir = 0; speed = 0;
            try
            {
                var cs = host?.GetType().GetProperty("cs")?.GetValue(host, null);
                if (cs == null) return false;
                var dirObj   = cs.GetType().GetProperty("wind_dir")?.GetValue(cs, null);
                var speedObj = cs.GetType().GetProperty("wind_vel")?.GetValue(cs, null);
                if (dirObj == null || speedObj == null) return false;
                dir   = Convert.ToSingle(dirObj,   CultureInfo.InvariantCulture);
                speed = Convert.ToSingle(speedObj, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        private static (double lat, double lon) OffsetLatLonByBearing(double startLat, double startLon, double bearingDeg, double distanceMeters)
        {
            const double R = 6378137.0;
            var b = bearingDeg * Math.PI / 180.0;
            var dLat = Math.Cos(b) * distanceMeters / R;
            var dLon = Math.Sin(b) * distanceMeters / (R * Math.Cos(startLat * Math.PI / 180.0));
            return (startLat + dLat * 180.0 / Math.PI, startLon + dLon * 180.0 / Math.PI);
        }

        private static double ComputeBearingDegrees(double fromLat, double fromLon, double toLat, double toLon)
        {
            var phi1 = fromLat * Math.PI / 180.0;
            var phi2 = toLat * Math.PI / 180.0;
            var dLon = (toLon - fromLon) * Math.PI / 180.0;
            var y = Math.Sin(dLon) * Math.Cos(phi2);
            var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
            var brng = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (brng < 0) brng += 360.0;
            return brng;
        }

        private void UpdateAutoDropPreview(double targetLat, double targetLon)
        {
            var input = new MissionWizardInput
            {
                DeliveryTargetLat = targetLat,
                DeliveryTargetLon = targetLon,
                UseDeliveryTarget = true,
                HasDeliveryPoint = true,
                DeliveryOnlyMission = true,
                AddPayloadRelease = true
            };

            MissionContextResolver.ApplyDefaults(host, input, resolveExternal: true);

            var xlsxPath = XlsxSearchPaths.FirstOrDefault(File.Exists);
            BombingTableSnapshot table = null;
            if (xlsxPath != null && BombingTableXlsx.TryLoad(xlsxPath, out var loaded, out _))
            {
                table = loaded;
            }

            if (table != null)
            {
                if (table.WindDirFromDeg.HasValue)
                {
                    input.WindDirectionFromDeg = table.WindDirFromDeg.Value;
                    input.WindSource = "XLSX";
                }

                if (table.WindSpeedMps.HasValue && table.WindSpeedMps.Value >= 0)
                {
                    input.WindSpeedMps = table.WindSpeedMps.Value;
                }

                var dropAlt = input.DropHeightAboveTargetMeters > 0
                    ? input.DropHeightAboveTargetMeters
                    : 100f;
                var releaseOffset = table.GetReleaseDistance(dropAlt);
                if (releaseOffset > 0)
                {
                    input.BombReleaseOffsetMeters = releaseOffset;
                }
            }

            // Always approach against wind, consistent with mission build logic.
            var inboundBearingDeg = input.WindDirectionFromDeg;

            var releaseLat = targetLat;
            var releaseLon = targetLon;
            if (input.BombReleaseOffsetMeters > 0.5f)
            {
                var upwindBearing = (inboundBearingDeg + 180.0) % 360.0;
                var rel = OffsetLatLonByBearing(targetLat, targetLon, upwindBearing, input.BombReleaseOffsetMeters);
                releaseLat = rel.lat;
                releaseLon = rel.lon;
            }

            _previewTargetLat = targetLat;
            _previewTargetLon = targetLon;
            _previewReleaseLat = releaseLat;
            _previewReleaseLon = releaseLon;
            _previewReleaseOffsetM = input.BombReleaseOffsetMeters;
            _previewWindDir = input.WindDirectionFromDeg;
            _previewWindSpeed = input.WindSpeedMps;
            _previewWindSource = input.WindSource ?? "Немає даних";
            _hasAutoDropPreview = true;
        }

        private static Button CreateBuilderButton(Control parent)
        {
            var btn = new Button
            {
                Width = 180, Height = 34, Text = "Балістика",
                BackColor = Color.FromArgb(29, 78, 216), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = Math.Max(10, parent.Width - 190),
                Top  = Math.Max(10, parent.Height - 46)
            };
            btn.FlatAppearance.BorderSize = 0;
            parent.Controls.Add(btn);
            btn.BringToFront();
            return btn;
        }

        private void OnBuilderButtonClick(object sender, EventArgs e)
        {
            if (autoMissionMode) { autoMissionMode = false; builderButton.Text = "Балістика"; builderButton.BackColor = ButtonNormalColor; return; }
            if (pickingMode)     { pickingMode = false; pickStep = 0; builderButton.Text = "Балістика"; builderButton.BackColor = ButtonNormalColor; return; }
            if (MissionPointsStore.HasStart && MissionPointsStore.HasDelivery && MissionPointsStore.HasLanding) { QueueOpenWizard(); return; }
            autoMissionMode = true;
            builderButton.Text = "▶ Оберіть ціль...";
            builderButton.BackColor = ButtonActiveColor;
        }

        private void OnMapMouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Left) return;

                PointLatLng p;
                try { p = map.FromLocalToLatLng(e.X, e.Y); }
                catch { return; }

                if (autoMissionMode)
                {
                    UpdateAutoDropPreview(p.Lat, p.Lng);
                    RefreshMarkers();

                    autoMissionMode = false;
                    builderButton.Text = "Балістика";
                    builderButton.BackColor = ButtonNormalColor;
                    AutoMissionService.ExecuteAsync(host, p.Lat, p.Lng);
                    return;
                }

                if (!pickingMode) return;

                if (pickStep == 0) { MissionPointsStore.SetStart(p.Lat, p.Lng); pickStep = 1; return; }
                if (pickStep == 1) { MissionPointsStore.SetDelivery(p.Lat, p.Lng); pickStep = 2; return; }

                MissionPointsStore.SetLanding(p.Lat, p.Lng);
                pickingMode = false; pickStep = 0;

                MessageBox.Show("Точки зібрано: СТАРТ, ДОСТАВКА, ПОСАДКА.\nЗараз відкриється Майстер місії.",
                    "Балістика", MessageBoxButtons.OK, MessageBoxIcon.Information);
                QueueOpenWizard();
            }
            catch (Exception ex)
            {
                autoMissionMode = false; pickingMode = false; pickStep = 0;
                builderButton.Text = "Балістика"; builderButton.BackColor = ButtonNormalColor;
                MessageBox.Show("Помилка обробки кліку на карті:\n" + ex.Message,
                    "Балістика", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnPointsChanged() { RefreshMarkers(); }

        private void QueueOpenWizard()
        {
            var uiTarget = host?.MainForm as Control;
            if (uiTarget != null && !uiTarget.IsDisposed)
                uiTarget.BeginInvoke(new Action(() => WizardDialogService.OpenWizard(host)));
            else
                WizardDialogService.OpenWizard(host);
        }

        public void ActivateBuilderMode()
        {
            builderButton?.PerformClick();
        }

        private static GMapMarker CreateMarker(double lat, double lon, string label, GMarkerGoogleType type)
        {
            return new GMarkerGoogle(new PointLatLng(lat, lon), type)
            {
                ToolTipText = label,
                ToolTipMode = MarkerTooltipMode.Always
            };
        }
    }
}