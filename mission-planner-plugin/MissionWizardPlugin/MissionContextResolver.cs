using System;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal static class MissionContextResolver
    {
        public static void ApplyDefaults(PluginHost host, MissionWizardInput input)
        {
            if (input == null)
            {
                return;
            }

            ApplyAutopilotDefaults(host, input);
            ApplyWind(host, input);

            if (input.TakeoffAltMeters <= 0)
            {
                input.TakeoffAltMeters = 100;
            }

            if (input.TakeoffPitchDegrees <= 0)
            {
                input.TakeoffPitchDegrees = 12;
            }

            if (input.DropHeightAboveTargetMeters <= 0)
            {
                input.DropHeightAboveTargetMeters = 100;
            }

            if (input.DeliveryRunInMeters <= 0)
            {
                input.DeliveryRunInMeters = 120;
            }

            if (input.LandingRunInMeters <= 0)
            {
                input.LandingRunInMeters = 180;
            }
        }

        private static void ApplyAutopilotDefaults(PluginHost host, MissionWizardInput input)
        {
            try
            {
                if (host == null)
                {
                    return;
                }

                if (!MissionPointsStore.HasStart)
                {
                    var home = host.GetType().GetProperty("cs")?.GetValue(host, null);
                    var homeLocation = home?.GetType().GetProperty("HomeLocation")?.GetValue(home, null);
                    if (TryReadLatLon(homeLocation, out var homeLat, out var homeLon))
                    {
                        input.HomeLat = homeLat;
                        input.HomeLon = homeLon;
                    }
                }

                input.TakeoffAltMeters = TryGetParamMeters(host, input.TakeoffAltMeters, 100,
                    ("TKOFF_ALT", 0.01f),
                    ("TKOFF_LVL_ALT", 1.0f));

                input.TakeoffPitchDegrees = TryGetParam(host, input.TakeoffPitchDegrees, 12,
                    ("TKOFF_LVL_PITCH", 1.0f),
                    ("TKOFF_PITCH_MIN", 1.0f),
                    ("PTCH_LIM_MAX_DEG", 1.0f));

                input.RtlAltMeters = TryGetParamMeters(host, input.RtlAltMeters, 100,
                    ("RTL_ALT", 0.01f),
                    ("Q_RTL_ALT", 0.01f),
                    ("ALT_HOLD_RTL", 1.0f));

                input.SpeedMetersPerSecond = TryGetParam(host, input.SpeedMetersPerSecond, 15,
                    ("AIRSPEED_CRUISE", 1.0f),
                    ("TRIM_ARSPD_CM", 0.01f),
                    ("WPNAV_SPEED", 0.01f));
            }
            catch
            {
                // Fallback to defaults if autopilot data is unavailable.
            }
        }

        private static void ApplyWind(PluginHost host, MissionWizardInput input)
        {
            if (TryGetWindFromAutopilot(host, out var dir, out var speed))
            {
                input.WindDirectionFromDeg = dir;
                input.WindSpeedMps = speed;
                input.WindSource = "Автопілот";
                return;
            }

            var lat = input.UseDeliveryTarget ? input.DeliveryTargetLat : input.HomeLat;
            var lon = input.UseDeliveryTarget ? input.DeliveryTargetLon : input.HomeLon;
            if (TryGetWindFromForecast(lat, lon, out dir, out speed))
            {
                input.WindDirectionFromDeg = dir;
                input.WindSpeedMps = speed;
                input.WindSource = "Open-Meteo";
                return;
            }

            input.WindDirectionFromDeg = 0;
            input.WindSpeedMps = 0;
            input.WindSource = "Немає даних";
        }

        private static bool TryGetWindFromAutopilot(PluginHost host, out float dir, out float speed)
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

                var windDirObj = cs.GetType().GetProperty("wind_dir")?.GetValue(cs, null);
                var windVelObj = cs.GetType().GetProperty("wind_vel")?.GetValue(cs, null);
                if (windDirObj == null || windVelObj == null)
                {
                    return false;
                }

                dir = Convert.ToSingle(windDirObj, CultureInfo.InvariantCulture);
                speed = Convert.ToSingle(windVelObj, CultureInfo.InvariantCulture);
                return speed > 0.1f;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetWindFromForecast(double lat, double lon, out float dir, out float speed)
        {
            dir = 0;
            speed = 0;

            try
            {
                var url = string.Format(CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=wind_speed_10m,wind_direction_10m&wind_speed_unit=ms",
                    lat,
                    lon);

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "MissionWizardPlugin/1.0";
                    var json = client.DownloadString(url);

                    var speedMatch = Regex.Match(json, @"""wind_speed_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    var dirMatch = Regex.Match(json, @"""wind_direction_10m""\s*:\s*(?<v>-?[0-9]+(?:\.[0-9]+)?)");
                    if (!speedMatch.Success || !dirMatch.Success)
                    {
                        return false;
                    }

                    speed = float.Parse(speedMatch.Groups["v"].Value, CultureInfo.InvariantCulture);
                    dir = float.Parse(dirMatch.Groups["v"].Value, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static float TryGetParamMeters(PluginHost host, float currentValue, float fallback, params (string name, float scale)[] candidates)
        {
            return TryGetParam(host, currentValue, fallback, candidates);
        }

        private static float TryGetParam(PluginHost host, float currentValue, float fallback, params (string name, float scale)[] candidates)
        {
            if (currentValue > 0.001f && Math.Abs(currentValue - fallback) > 0.001f)
            {
                return currentValue;
            }

            foreach (var candidate in candidates)
            {
                if (TryReadParam(host, candidate.name, out var value))
                {
                    var scaled = value * candidate.scale;
                    if (scaled > 0)
                    {
                        return scaled;
                    }
                }
            }

            return currentValue > 0 ? currentValue : fallback;
        }

        private static bool TryReadParam(PluginHost host, string paramName, out float value)
        {
            value = 0;

            try
            {
                var comPort = host?.GetType().GetProperty("comPort")?.GetValue(host, null);
                if (comPort == null)
                {
                    return false;
                }

                var getParam = comPort.GetType().GetMethod("GetParam", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                if (getParam == null)
                {
                    return false;
                }

                var result = getParam.Invoke(comPort, new object[] { paramName });
                if (result == null)
                {
                    return false;
                }

                value = Convert.ToSingle(result, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadLatLon(object pointObj, out double lat, out double lon)
        {
            lat = 0;
            lon = 0;

            try
            {
                if (pointObj == null)
                {
                    return false;
                }

                var type = pointObj.GetType();
                var latObj = type.GetProperty("Lat")?.GetValue(pointObj, null);
                var lonObj = type.GetProperty("Lng")?.GetValue(pointObj, null)
                    ?? type.GetProperty("Lon")?.GetValue(pointObj, null)
                    ?? type.GetProperty("Lng")?.GetValue(pointObj, null);
                if (latObj == null || lonObj == null)
                {
                    return false;
                }

                lat = Convert.ToDouble(latObj, CultureInfo.InvariantCulture);
                lon = Convert.ToDouble(lonObj, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
