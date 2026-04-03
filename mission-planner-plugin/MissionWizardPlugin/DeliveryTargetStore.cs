namespace MissionWizardPlugin
{
    internal static class DeliveryTargetStore
    {
        public static bool HasTarget { get; private set; }
        public static double TargetLat { get; private set; }
        public static double TargetLon { get; private set; }

        public static void Set(double lat, double lon)
        {
            TargetLat = lat;
            TargetLon = lon;
            HasTarget = true;
        }

        public static void Clear()
        {
            HasTarget = false;
            TargetLat = 0;
            TargetLon = 0;
        }
    }
}
