using System.Collections.Generic;

namespace MissionWizardPlugin
{
    public sealed class MissionWizardInput
    {
        public double HomeLat { get; set; } = 50.4501;
        public double HomeLon { get; set; } = 30.5234;
        public float TakeoffAltMeters { get; set; } = 100;
        public float TakeoffPitchDegrees { get; set; } = 12;
        public float CruiseAltMeters { get; set; } = 80;
        public float RtlAltMeters { get; set; } = 100;

        public double AreaCenterLat { get; set; } = 50.4501;
        public double AreaCenterLon { get; set; } = 30.5234;
        public float AreaWidthMeters { get; set; } = 500;
        public float AreaHeightMeters { get; set; } = 300;
        public float LaneSpacingMeters { get; set; } = 60;
        public float YawDegrees { get; set; } = 0;

        public float SpeedMetersPerSecond { get; set; } = 15;
        public bool AddCameraTrigger { get; set; } = false;
        public float CameraTriggerMeters { get; set; } = 40;

        public bool UsePointRoute { get; set; } = false;
        public bool HasDeliveryPoint { get; set; } = false;
        public bool HasLandingPoint { get; set; } = false;
        public double LandingLat { get; set; }
        public double LandingLon { get; set; }

        public bool UseDeliveryTarget { get; set; } = false;
        public bool DeliveryOnlyMission { get; set; } = true;
        public double DeliveryTargetLat { get; set; }
        public double DeliveryTargetLon { get; set; }
        public float DeliveryTargetRelativeAltMeters { get; set; } = 0;
        public float DropHeightAboveTargetMeters { get; set; } = 100;
        public float DeliveryRunInMeters { get; set; } = 120;
        public float PostDropEgressMeters { get; set; } = 1000;
        // Відносна дистанція сброса з таблиці бомбометання (метри). 0 = не застосовується.
        public float BombReleaseOffsetMeters { get; set; } = 0;
        public float LandingRunInMeters { get; set; } = 180;
        public float LandingRelativeAltMeters { get; set; } = 0;
        public bool AddPayloadRelease { get; set; } = true;
        public float PayloadReleaseDelaySeconds { get; set; } = 0;
        public int PayloadServoNumber { get; set; } = 9;
        public int PayloadServoPwm { get; set; } = 1900;
        public float WindDirectionFromDeg { get; set; } = 0;
        public float WindSpeedMps { get; set; } = 0;
        public string WindSource { get; set; } = "Немає даних";

        public IList<MissionItem> BuildMissionItems()
        {
            return MissionBuilder.Build(this);
        }
    }

    public sealed class MissionItem
    {
        public int Seq { get; set; }
        public int Frame { get; set; } = 3;
        public int Command { get; set; }
        public float Param1 { get; set; }
        public float Param2 { get; set; }
        public float Param3 { get; set; }
        public float Param4 { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public float Alt { get; set; }
        public bool Current { get; set; }
        public bool AutoContinue { get; set; } = true;
    }
}
