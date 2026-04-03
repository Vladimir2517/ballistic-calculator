using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MissionWizardPlugin
{
    internal static class MissionBuilder
    {
        private const double EarthRadiusMeters = 6378137.0;

        // MAVLink command ids
        private const int CmdNavTakeoff = 22;
        private const int CmdNavWaypoint = 16;
        private const int CmdDoChangeSpeed = 178;
        private const int CmdDoSetCamTrigDist = 206;
        private const int CmdNavReturnToLaunch = 20;

        public static IList<MissionItem> Build(MissionWizardInput input)
        {
            if (input.LaneSpacingMeters <= 0)
            {
                throw new InvalidOperationException("Lane spacing must be > 0");
            }

            var mission = new List<MissionItem>();
            var seq = 0;

            mission.Add(new MissionItem
            {
                Seq = seq++,
                Current = true,
                Command = CmdNavTakeoff,
                Lat = input.HomeLat,
                Lon = input.HomeLon,
                Alt = input.TakeoffAltMeters,
                Param1 = 0,
                Param4 = input.YawDegrees
            });

            mission.Add(new MissionItem
            {
                Seq = seq++,
                Command = CmdDoChangeSpeed,
                Param1 = 1,
                Param2 = input.SpeedMetersPerSecond,
                Lat = 0,
                Lon = 0,
                Alt = 0
            });

            if (input.AddCameraTrigger)
            {
                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdDoSetCamTrigDist,
                    Param1 = input.CameraTriggerMeters,
                    Lat = 0,
                    Lon = 0,
                    Alt = 0
                });
            }

            var waypoints = BuildLawnmowerPattern(
                input.AreaCenterLat,
                input.AreaCenterLon,
                input.AreaWidthMeters,
                input.AreaHeightMeters,
                input.LaneSpacingMeters,
                input.CruiseAltMeters,
                input.YawDegrees);

            foreach (var wp in waypoints)
            {
                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavWaypoint,
                    Lat = wp.lat,
                    Lon = wp.lon,
                    Alt = wp.alt
                });
            }

            mission.Add(new MissionItem
            {
                Seq = seq,
                Command = CmdNavReturnToLaunch,
                Param1 = 0,
                Lat = 0,
                Lon = 0,
                Alt = input.RtlAltMeters
            });

            return mission;
        }

        public static string WriteQgcWpl(IList<MissionItem> mission, string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var fileName = $"mission_{DateTime.UtcNow:yyyyMMdd_HHmmss}.waypoints";
            var fullPath = Path.Combine(outputDir, fileName);

            var lines = new List<string> { "QGC WPL 110" };
            var ci = CultureInfo.InvariantCulture;

            foreach (var item in mission.OrderBy(m => m.Seq))
            {
                var line = string.Format(ci,
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}",
                    item.Seq,
                    item.Current ? 1 : 0,
                    item.Frame,
                    item.Command,
                    item.Param1,
                    item.Param2,
                    item.Param3,
                    item.Param4,
                    item.Lat,
                    item.Lon,
                    item.Alt,
                    item.AutoContinue ? 1 : 0);

                lines.Add(line);
            }

            File.WriteAllLines(fullPath, lines, Encoding.UTF8);
            return fullPath;
        }

        private static List<(double lat, double lon, float alt)> BuildLawnmowerPattern(
            double centerLat,
            double centerLon,
            float widthMeters,
            float heightMeters,
            float laneSpacingMeters,
            float altMeters,
            float yawDegrees)
        {
            var halfW = widthMeters / 2.0;
            var halfH = heightMeters / 2.0;
            var lanes = Math.Max(2, (int)Math.Ceiling(widthMeters / laneSpacingMeters) + 1);

            var laneXs = new List<double>();
            for (var i = 0; i < lanes; i++)
            {
                var t = lanes == 1 ? 0 : (double)i / (lanes - 1);
                laneXs.Add(-halfW + (t * widthMeters));
            }

            var pointsLocal = new List<(double x, double y)>();
            var top = halfH;
            var bottom = -halfH;

            for (var i = 0; i < laneXs.Count; i++)
            {
                var x = laneXs[i];
                if (i % 2 == 0)
                {
                    pointsLocal.Add((x, top));
                    pointsLocal.Add((x, bottom));
                }
                else
                {
                    pointsLocal.Add((x, bottom));
                    pointsLocal.Add((x, top));
                }
            }

            var angle = DegreesToRadians(yawDegrees);
            var outPoints = new List<(double lat, double lon, float alt)>();

            foreach (var p in pointsLocal)
            {
                var rx = (p.x * Math.Cos(angle)) - (p.y * Math.Sin(angle));
                var ry = (p.x * Math.Sin(angle)) + (p.y * Math.Cos(angle));

                var (lat, lon) = OffsetLatLon(centerLat, centerLon, rx, ry);
                outPoints.Add((lat, lon, altMeters));
            }

            return outPoints;
        }

        private static (double lat, double lon) OffsetLatLon(double latDeg, double lonDeg, double eastMeters, double northMeters)
        {
            var dLat = northMeters / EarthRadiusMeters;
            var dLon = eastMeters / (EarthRadiusMeters * Math.Cos(DegreesToRadians(latDeg)));

            var latOut = latDeg + RadiansToDegrees(dLat);
            var lonOut = lonDeg + RadiansToDegrees(dLon);
            return (latOut, lonOut);
        }

        private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
        private static double RadiansToDegrees(double rad) => rad * 180.0 / Math.PI;
    }
}
