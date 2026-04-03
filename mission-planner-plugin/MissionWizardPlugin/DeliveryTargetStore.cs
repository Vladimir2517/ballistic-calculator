namespace MissionWizardPlugin
{
    internal static class MissionPointsStore
    {
        public static bool HasStart { get; private set; }
        public static bool HasDelivery { get; private set; }
        public static bool HasLanding { get; private set; }

        public static double StartLat { get; private set; }
        public static double StartLon { get; private set; }
        public static double DeliveryLat { get; private set; }
        public static double DeliveryLon { get; private set; }
        public static double LandingLat { get; private set; }
        public static double LandingLon { get; private set; }

        public static void SetStart(double lat, double lon)
        {
            StartLat = lat;
            StartLon = lon;
            HasStart = true;
        }

        public static void SetDelivery(double lat, double lon)
        {
            DeliveryLat = lat;
            DeliveryLon = lon;
            HasDelivery = true;
        }

        public static void SetLanding(double lat, double lon)
        {
            LandingLat = lat;
            LandingLon = lon;
            HasLanding = true;
        }

        public static void ClearAll()
        {
            HasStart = false;
            HasDelivery = false;
            HasLanding = false;

            StartLat = 0;
            StartLon = 0;
            DeliveryLat = 0;
            DeliveryLon = 0;
            LandingLat = 0;
            LandingLon = 0;
        }
    }
}
