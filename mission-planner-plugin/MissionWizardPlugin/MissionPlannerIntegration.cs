using System;
using System.Reflection;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal static class MissionPlannerIntegration
    {
        public static void LoadWaypointsIntoFlightPlanner(PluginHost host, string waypointFile)
        {
            if (host == null)
            {
                throw new InvalidOperationException("Plugin host is not available.");
            }

            if (string.IsNullOrWhiteSpace(waypointFile))
            {
                throw new ArgumentException("Waypoint file path is empty.", nameof(waypointFile));
            }

            var mainForm = host.MainForm;
            if (mainForm == null)
            {
                throw new InvalidOperationException("Mission Planner main form is not available.");
            }

            // Open Flight Planner tab for user context.
            var menuFlightPlannerField = mainForm.GetType().GetField("MenuFlightPlanner",
                BindingFlags.Public | BindingFlags.Instance);
            var menuFlightPlanner = menuFlightPlannerField?.GetValue(mainForm) as System.Windows.Forms.ToolStripButton;
            menuFlightPlanner?.PerformClick();

            var flightPlannerField = mainForm.GetType().GetField("FlightPlanner",
                BindingFlags.Public | BindingFlags.Instance);
            var flightPlanner = flightPlannerField?.GetValue(mainForm);
            if (flightPlanner == null)
            {
                throw new InvalidOperationException("FlightPlanner view is not available.");
            }

            var readMethod = flightPlanner.GetType().GetMethod("readQGC110wpfile",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(bool) },
                null);

            if (readMethod == null)
            {
                throw new MissingMethodException("Could not find FlightPlanner.readQGC110wpfile(string,bool)");
            }

            // append=false replaces current mission list in Flight Plan.
            readMethod.Invoke(flightPlanner, new object[] { waypointFile, false });
        }
    }
}
