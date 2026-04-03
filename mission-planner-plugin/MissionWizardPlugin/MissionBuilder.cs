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

        private const int CmdNavTakeoff = 22;
        private const int CmdNavWaypoint = 16;
        private const int CmdDoChangeSpeed = 178;
        private const int CmdDoSetCamTrigDist = 206;
        private const int CmdDoSetServo = 183;
        private const int CmdNavDelay = 93;
        private const int CmdNavLand = 21;
        private const int CmdNavReturnToLaunch = 20;
        public static IList<MissionItem> Build(MissionWizardInput input)
        {
            if (input.LaneSpacingMeters <= 0)
            {
                throw new InvalidOperationException("Крок між проходами має бути більшим за 0");
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
                Param1 = input.TakeoffPitchDegrees,
                Param4 = input.WindSpeedMps > 0.1f ? input.WindDirectionFromDeg : input.YawDegrees
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

            if (!(input.UseDeliveryTarget && input.DeliveryOnlyMission) && !input.UsePointRoute)
            {
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
            }

            if (input.UseDeliveryTarget)
            {
                var deliveryAlt = input.DeliveryTargetRelativeAltMeters + input.DropHeightAboveTargetMeters;
                double inboundBearingDeg;
                var deliveryApproach = input.WindSpeedMps > 0.1f
                    ? BuildApproachPointAgainstWind(
                        input.DeliveryTargetLat,
                        input.DeliveryTargetLon,
                        input.WindDirectionFromDeg,
                        input.DeliveryRunInMeters)
                    : BuildApproachPoint(
                        input.HomeLat,
                        input.HomeLon,
                        input.DeliveryTargetLat,
                        input.DeliveryTargetLon,
                        input.DeliveryRunInMeters);

                inboundBearingDeg = input.WindSpeedMps > 0.1f
                    ? input.WindDirectionFromDeg
                    : ComputeBearingDegrees(
                        deliveryApproach.lat,
                        deliveryApproach.lon,
                        input.DeliveryTargetLat,
                        input.DeliveryTargetLon);

                var deliveryEgress = OffsetLatLonByBearing(
                    input.DeliveryTargetLat,
                    input.DeliveryTargetLon,
                    inboundBearingDeg,
                    input.PostDropEgressMeters);

                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavWaypoint,
                    Lat = deliveryApproach.lat,
                    Lon = deliveryApproach.lon,
                    Alt = deliveryAlt
                });

                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavWaypoint,
                    Lat = input.DeliveryTargetLat,
                    Lon = input.DeliveryTargetLon,
                    Alt = deliveryAlt
                });

                if (input.AddPayloadRelease)
                {
                    mission.Add(new MissionItem
                    {
                        Seq = seq++,
                        Command = CmdDoSetServo,
                        Param1 = input.PayloadServoNumber,
                        Param2 = input.PayloadServoPwm,
                        Lat = 0,
                        Lon = 0,
                        Alt = 0
                    });

                    if (input.PayloadReleaseDelaySeconds > 0)
                    {
                        mission.Add(new MissionItem
                        {
                            Seq = seq++,
                            Command = CmdNavDelay,
                            Param1 = input.PayloadReleaseDelaySeconds,
                            Lat = input.DeliveryTargetLat,
                            Lon = input.DeliveryTargetLon,
                            Alt = deliveryAlt
                        });
                    }
                }

                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavWaypoint,
                    Lat = deliveryEgress.lat,
                    Lon = deliveryEgress.lon,
                    Alt = deliveryAlt
                });
            }

            if (input.UsePointRoute)
            {
                if (!input.HasDeliveryPoint)
                {
                    throw new InvalidOperationException("Точку доставки не встановлено.");
                }

                if (!input.HasLandingPoint)
                {
                    throw new InvalidOperationException("Точку посадки не встановлено.");
                }

                var landingApproach = input.WindSpeedMps > 0.1f
                    ? BuildApproachPointAgainstWind(
                        input.LandingLat,
                        input.LandingLon,
                        input.WindDirectionFromDeg,
                        input.LandingRunInMeters)
                    : BuildApproachPoint(
                        input.DeliveryTargetLat,
                        input.DeliveryTargetLon,
                        input.LandingLat,
                        input.LandingLon,
                        input.LandingRunInMeters);

                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavWaypoint,
                    Lat = landingApproach.lat,
                    Lon = landingApproach.lon,
                    Alt = input.CruiseAltMeters
                });

                mission.Add(new MissionItem
                {
                    Seq = seq++,
                    Command = CmdNavLand,
                    Lat = input.LandingLat,
                    Lon = input.LandingLon,
                    Alt = input.LandingRelativeAltMeters
                });

                return mission;
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

        private static (double lat, double lon) BuildApproachPoint(
            double fromLat,
            double fromLon,
            double targetLat,
            double targetLon,
            float runInMeters)
        {
            var dLat = DegreesToRadians(targetLat - fromLat);
            var dLon = DegreesToRadians(targetLon - fromLon);
            var lat1 = DegreesToRadians(fromLat);
            var lat2 = DegreesToRadians(targetLat);

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var bearing = Math.Atan2(y, x);

            var north = -Math.Cos(bearing) * runInMeters;
            var east = -Math.Sin(bearing) * runInMeters;
            return OffsetLatLon(targetLat, targetLon, east, north);
        }

        private static (double lat, double lon) BuildApproachPointAgainstWind(
            double targetLat,
            double targetLon,
            float windFromDeg,
            float runInMeters)
        {
            var bearing = DegreesToRadians(windFromDeg);
            var north = -Math.Cos(bearing) * runInMeters;
            var east = -Math.Sin(bearing) * runInMeters;
            return OffsetLatLon(targetLat, targetLon, east, north);
        }

        private static (double lat, double lon) OffsetLatLonByBearing(
            double startLat,
            double startLon,
            double bearingDeg,
            double distanceMeters)
        {
            var bearing = DegreesToRadians(bearingDeg);
            var north = Math.Cos(bearing) * distanceMeters;
            var east = Math.Sin(bearing) * distanceMeters;
            return OffsetLatLon(startLat, startLon, east, north);
        }

        private static double ComputeBearingDegrees(
            double fromLat,
            double fromLon,
            double toLat,
            double toLon)
        {
            var dLon = DegreesToRadians(toLon - fromLon);
            var lat1 = DegreesToRadians(fromLat);
            var lat2 = DegreesToRadians(toLat);

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var bearing = RadiansToDegrees(Math.Atan2(y, x));
            return (bearing + 360.0) % 360.0;
        }

        private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
        private static double RadiansToDegrees(double rad) => rad * 180.0 / Math.PI;
    }
}
