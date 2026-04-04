using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.GCSViews;
using MissionPlanner.Plugin;

namespace RadarPlugin
{
    internal sealed class RadarOverlayController : IDisposable
    {
        private const string ThreatsUrl = "http://127.0.0.1:8081/api/threats";

        private readonly PluginHost host;
        private readonly Timer refreshTimer;
        private GMapOverlay radarOverlay;
        private GMapControl map;
        private bool isEnabled;

        public RadarOverlayController(PluginHost host)
        {
            this.host = host;
            refreshTimer = new Timer { Interval = 5000 };
            refreshTimer.Tick += (_, __) => RefreshMarkers();
        }

        public void Dispose()
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();

            if (map != null && radarOverlay != null)
            {
                try
                {
                    radarOverlay.Markers.Clear();
                    radarOverlay.Routes.Clear();
                    if (map.Overlays.Contains(radarOverlay))
                    {
                        map.Overlays.Remove(radarOverlay);
                    }
                }
                catch
                {
                }
            }

            radarOverlay = null;
            map = null;
        }

        public void ActivateRadarTab()
        {
            OpenFlightData();
            EnsureMapAndOverlay();

            isEnabled = true;
            refreshTimer.Start();
            RefreshMarkers();
        }

        private void OpenFlightData()
        {
            var mainForm = host?.MainForm;
            if (mainForm == null)
            {
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var menuFlightDataField = mainForm.GetType().GetField("MenuFlightData", flags);
            var menuFlightData = menuFlightDataField?.GetValue(mainForm) as ToolStripItem;
            menuFlightData?.PerformClick();
        }

        private void EnsureMapAndOverlay()
        {
            if (map == null)
            {
                map = FlightData.instance?.gMapControl1 ?? host?.FPGMapControl;
            }

            if (map == null)
            {
                return;
            }

            if (radarOverlay == null)
            {
                radarOverlay = new GMapOverlay("RadarPluginOverlay");
            }

            if (!map.Overlays.Contains(radarOverlay))
            {
                map.Overlays.Add(radarOverlay);
            }
        }

        private void RefreshMarkers()
        {
            if (!isEnabled)
            {
                return;
            }

            EnsureMapAndOverlay();
            if (map == null || radarOverlay == null)
            {
                return;
            }

            try
            {
                var threats = FetchThreats();

                radarOverlay.Markers.Clear();
                foreach (var t in threats)
                {
                    var marker = new GMarkerGoogle(new PointLatLng(t.Lat, t.Lon), GMarkerGoogleType.red_dot)
                    {
                        ToolTipText = t.Title,
                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                    };
                    radarOverlay.Markers.Add(marker);
                }

                map.Refresh();
            }
            catch
            {
                // Keep plugin stable if backend is temporarily unavailable.
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

        private static List<ThreatMarker> ExtractThreatMarkers(object root)
        {
            var result = new List<ThreatMarker>();

            if (root is object[] arr)
            {
                foreach (var item in arr)
                {
                    TryAddThreat(item, result);
                }
                return result;
            }

            if (root is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("threats", out var threatsObj) && threatsObj is object[] threatsArr)
                {
                    foreach (var item in threatsArr)
                    {
                        TryAddThreat(item, result);
                    }
                    return result;
                }

                if (dict.TryGetValue("items", out var itemsObj) && itemsObj is object[] itemsArr)
                {
                    foreach (var item in itemsArr)
                    {
                        TryAddThreat(item, result);
                    }
                    return result;
                }
            }

            return result;
        }

        private static void TryAddThreat(object item, ICollection<ThreatMarker> output)
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

            var title = TryGetString(d, "title", "name", "callsign", "id", "type") ?? "Threat";
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
                {
                    return dd;
                }
                if (value is float ff)
                {
                    return ff;
                }
                if (value is int ii)
                {
                    return ii;
                }

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

        private sealed class ThreatMarker
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Title { get; set; }
        }
    }
}
