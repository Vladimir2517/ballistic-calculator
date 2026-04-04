using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.GCSViews;

namespace RadarPlugin
{
    internal sealed class RadarEmbeddedView : UserControl
    {
        private const string ThreatsUrl = "http://127.0.0.1:8081/api/threats";
        private const string CivilAdsbUrl = "http://127.0.0.1:8081/api/civil_adsb/airplanes";
        private const string OwnFpvUrl = "http://127.0.0.1:8081/api/fpv/own";

        private readonly GMapControl map;
        private readonly GMapOverlay threatsOverlay;
        private readonly GMapOverlay civilOverlay;
        private readonly GMapOverlay ownFpvOverlay;
        private readonly Label statusLabel;
        private readonly Timer refreshTimer;
        private volatile bool refreshInProgress;
        private int lastDataFingerprint;
        private bool hasRenderedData;

        public RadarEmbeddedView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(28, 28, 28);

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(36, 36, 36)
            };

            var titleLabel = new Label
            {
                Left = 10,
                Top = 12,
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Text = "RADAR"
            };

            var refreshButton = new Button
            {
                Text = "Обновить",
                Left = 90,
                Top = 7,
                Width = 100,
                Height = 26,
                BackColor = Color.FromArgb(64, 64, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += (_, __) => StartRefresh(forceUiUpdate: true);

            statusLabel = new Label
            {
                Left = 200,
                Top = 12,
                Width = 1000,
                ForeColor = Color.FromArgb(180, 180, 180),
                Text = "Инициализация карты..."
            };

            topPanel.Controls.Add(titleLabel);
            topPanel.Controls.Add(refreshButton);
            topPanel.Controls.Add(statusLabel);

            map = new GMapControl
            {
                Dock = DockStyle.Fill,
                MapProvider = OpenStreetMapProvider.Instance,
                MinZoom = 2,
                MaxZoom = 19,
                Zoom = 6,
                DragButton = MouseButtons.Left,
                CanDragMap = true,
                ShowCenter = false,
                GrayScaleMode = false,
                MarkersEnabled = true,
                PolygonsEnabled = true,
                RoutesEnabled = true,
                Position = new PointLatLng(49.0, 31.3)
            };

            ApplyMissionPlannerMapStyle();

            threatsOverlay = new GMapOverlay("RadarThreats");
            civilOverlay = new GMapOverlay("RadarCivil");
            ownFpvOverlay = new GMapOverlay("RadarOwnFpv");
            map.Overlays.Add(civilOverlay);
            map.Overlays.Add(ownFpvOverlay);
            map.Overlays.Add(threatsOverlay);

            Controls.Add(map);
            Controls.Add(topPanel);

            refreshTimer = new Timer { Interval = 5000 };
            refreshTimer.Tick += (_, __) => StartRefresh(forceUiUpdate: false);

            VisibleChanged += (_, __) =>
            {
                if (Visible)
                {
                    refreshTimer.Start();
                    StartRefresh(forceUiUpdate: true);
                }
                else
                {
                    refreshTimer.Stop();
                }
            };

            Disposed += (_, __) =>
            {
                refreshTimer.Stop();
                refreshTimer.Dispose();
            };
        }

        public void ActivateView()
        {
            ApplyMissionPlannerMapStyle();
            refreshTimer.Start();
            StartRefresh(forceUiUpdate: true);
            BringToFront();
        }

        private void ApplyMissionPlannerMapStyle()
        {
            try
            {
                var mpMap = FlightData.instance?.gMapControl1;
                if (mpMap == null)
                {
                    return;
                }

                if (mpMap.MapProvider != null)
                {
                    map.MapProvider = mpMap.MapProvider;
                }

                map.MinZoom = mpMap.MinZoom;
                map.MaxZoom = mpMap.MaxZoom;
                map.Zoom = mpMap.Zoom;
                map.Position = mpMap.Position;
                map.CanDragMap = mpMap.CanDragMap;
                map.DragButton = mpMap.DragButton;
                map.GrayScaleMode = mpMap.GrayScaleMode;
                map.MarkersEnabled = mpMap.MarkersEnabled;
                map.PolygonsEnabled = mpMap.PolygonsEnabled;
                map.RoutesEnabled = mpMap.RoutesEnabled;
                map.ShowCenter = mpMap.ShowCenter;
                map.EmptyTileColor = mpMap.EmptyTileColor;
                map.RetryLoadTile = mpMap.RetryLoadTile;
            }
            catch
            {
            }
        }

        private void StartRefresh(bool forceUiUpdate)
        {
            if (refreshInProgress)
            {
                return;
            }

            refreshInProgress = true;
            statusLabel.Text = "Обновление...";

            Task.Run(() =>
            {
                try
                {
                    var snapshot = FetchSnapshot();
                    var fingerprint = BuildDataFingerprint(snapshot);

                    if (!forceUiUpdate && hasRenderedData && fingerprint == lastDataFingerprint)
                    {
                        var unchangedText = string.Format(
                            CultureInfo.InvariantCulture,
                            "Угрозы: {0} | ADS-B: {1} | FPV: {2} | Без изменений: {3:HH:mm:ss}",
                            snapshot.Threats.Count,
                            snapshot.Civil.Count,
                            snapshot.OwnFpv.Count,
                            DateTime.Now);
                        ApplyStatusSafe(unchangedText);
                        return;
                    }

                    ApplyMarkersSafe(snapshot, fingerprint);
                }
                catch (Exception ex)
                {
                    ApplyStatusSafe("Ошибка: " + ex.Message);
                }
                finally
                {
                    refreshInProgress = false;
                }
            });
        }

        private void ApplyMarkersSafe(RadarSnapshot snapshot, int fingerprint)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke((MethodInvoker)delegate
            {
                if (IsDisposed)
                {
                    return;
                }

                threatsOverlay.Markers.Clear();
                foreach (var t in snapshot.Threats)
                {
                    var marker = new GMarkerGoogle(new PointLatLng(t.Lat, t.Lon), GMarkerGoogleType.red_dot)
                    {
                        ToolTipText = t.Title,
                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                    };
                    threatsOverlay.Markers.Add(marker);
                }

                civilOverlay.Markers.Clear();
                foreach (var c in snapshot.Civil)
                {
                    var marker = new GMarkerGoogle(new PointLatLng(c.Lat, c.Lon), GMarkerGoogleType.blue_small)
                    {
                        ToolTipText = c.Title,
                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                    };
                    civilOverlay.Markers.Add(marker);
                }

                ownFpvOverlay.Markers.Clear();
                foreach (var o in snapshot.OwnFpv)
                {
                    var marker = new GMarkerGoogle(new PointLatLng(o.Lat, o.Lon), GMarkerGoogleType.green_dot)
                    {
                        ToolTipText = o.Title,
                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                    };
                    ownFpvOverlay.Markers.Add(marker);
                }

                hasRenderedData = true;
                lastDataFingerprint = fingerprint;
                statusLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Угрозы: {0} | ADS-B: {1} | FPV: {2} | Обновлено: {3:HH:mm:ss}",
                    snapshot.Threats.Count,
                    snapshot.Civil.Count,
                    snapshot.OwnFpv.Count,
                    DateTime.Now);
                map.Refresh();
            });
        }

        private void ApplyStatusSafe(string message)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke((MethodInvoker)delegate
            {
                if (IsDisposed)
                {
                    return;
                }

                statusLabel.Text = message;
            });
        }

        private static int BuildDataFingerprint(RadarSnapshot snapshot)
        {
            unchecked
            {
                var hash = 17;
                hash = HashThreats(hash, snapshot.Threats);
                hash = HashThreats(hash, snapshot.Civil);
                hash = HashThreats(hash, snapshot.OwnFpv);

                return hash;
            }
        }

        private static int HashThreats<T>(int seed, IList<T> items) where T : ILatLonTitle
        {
            unchecked
            {
                var hash = seed * 31 + items.Count;
                for (var i = 0; i < items.Count; i++)
                {
                    hash = hash * 31 + items[i].Lat.GetHashCode();
                    hash = hash * 31 + items[i].Lon.GetHashCode();
                    hash = hash * 31 + (items[i].Title ?? string.Empty).GetHashCode();
                }
                return hash;
            }
        }

        private static List<ThreatMarker> FetchThreats()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var req = (HttpWebRequest)WebRequest.Create(ThreatsUrl);
            req.Timeout = 5000;
            req.UserAgent = "RadarPlugin/1.0";

            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                var json = sr.ReadToEnd();
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.DeserializeObject(json);
                return ExtractThreatMarkers(root);
            }
        }

        private static RadarSnapshot FetchSnapshot()
        {
            var snapshot = new RadarSnapshot();

            try { snapshot.Threats = FetchThreats(); } catch { snapshot.Threats = new List<ThreatMarker>(); }
            try { snapshot.Civil = FetchMarkersFromEndpoint(CivilAdsbUrl, "Civil"); } catch { snapshot.Civil = new List<ThreatMarker>(); }
            try { snapshot.OwnFpv = FetchMarkersFromEndpoint(OwnFpvUrl, "OwnFPV"); } catch { snapshot.OwnFpv = new List<ThreatMarker>(); }

            return snapshot;
        }

        private static List<ThreatMarker> FetchMarkersFromEndpoint(string url, string fallbackTitle)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 5000;
            req.UserAgent = "RadarPlugin/1.0";

            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                var json = sr.ReadToEnd();
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.DeserializeObject(json);
                return ExtractThreatMarkers(root, fallbackTitle);
            }
        }

        private static List<ThreatMarker> ExtractThreatMarkers(object root)
        {
            return ExtractThreatMarkers(root, "Threat");
        }

        private static List<ThreatMarker> ExtractThreatMarkers(object root, string fallbackTitle)
        {
            var result = new List<ThreatMarker>();

            if (root is object[] arr)
            {
                foreach (var item in arr)
                {
                    TryAddThreat(item, result, fallbackTitle);
                }
                return result;
            }

            if (root is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("threats", out var threatsObj) && threatsObj is object[] threatsArr)
                {
                    foreach (var item in threatsArr)
                    {
                        TryAddThreat(item, result, fallbackTitle);
                    }
                    return result;
                }

                if (dict.TryGetValue("items", out var itemsObj) && itemsObj is object[] itemsArr)
                {
                    foreach (var item in itemsArr)
                    {
                        TryAddThreat(item, result, fallbackTitle);
                    }
                    return result;
                }
            }

            return result;
        }

        private static void TryAddThreat(object item, ICollection<ThreatMarker> output, string fallbackTitle)
        {
            if (!(item is Dictionary<string, object> d))
            {
                return;
            }

            var lat = TryGetNumber(d, "lat", "latitude", "y");
            var lon = TryGetNumber(d, "lon", "lng", "longitude", "x");
            if (!lat.HasValue || !lon.HasValue)
            {
                return;
            }

            var title = TryGetString(d, "title", "name", "callsign", "id", "type") ?? fallbackTitle;
            output.Add(new ThreatMarker
            {
                Lat = lat.Value,
                Lon = lon.Value,
                Title = title
            });
        }

        private static double? TryGetNumber(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!d.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is double dd)
                    return dd;
                if (value is float ff)
                    return ff;
                if (value is int ii)
                    return ii;

                if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static string TryGetString(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!d.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                var s = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }
            }

            return null;
        }

        private interface ILatLonTitle
        {
            double Lat { get; }
            double Lon { get; }
            string Title { get; }
        }

        private sealed class ThreatMarker : ILatLonTitle
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Title { get; set; }
        }

        private sealed class RadarSnapshot
        {
            public List<ThreatMarker> Threats { get; set; } = new List<ThreatMarker>();
            public List<ThreatMarker> Civil { get; set; } = new List<ThreatMarker>();
            public List<ThreatMarker> OwnFpv { get; set; } = new List<ThreatMarker>();
        }
    }
}
