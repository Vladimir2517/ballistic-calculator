using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace RadarPlugin
{
    internal static class AlphaLauncher
    {
        public static bool TryLaunchOrActivate(PluginHost host, out string message)
        {
            message = null;

            try
            {
                if (host == null)
                {
                    message = "PluginHost Mission Planner недоступний.";
                    return false;
                }

                OpenFlightData(host);

                var alphaMenu = FindAlphaMenu(host);
                if (alphaMenu == null)
                {
                    message = "Не знайдено меню Alpha Map у плагінах Mission Planner.";
                    return false;
                }

                var connectItem = alphaMenu.DropDownItems
                    .OfType<ToolStripItem>()
                    .FirstOrDefault(item => string.Equals((item.Text ?? string.Empty).Trim(), "Подключиться", StringComparison.OrdinalIgnoreCase));

                var refreshItem = alphaMenu.DropDownItems
                    .OfType<ToolStripItem>()
                    .FirstOrDefault(item => string.Equals((item.Text ?? string.Empty).Trim(), "Refresh Now", StringComparison.OrdinalIgnoreCase));

                if (connectItem != null)
                {
                    connectItem.PerformClick();
                }

                if (refreshItem != null)
                {
                    refreshItem.PerformClick();
                }

                message = connectItem != null
                    ? "Alpha Map відкрито в Mission Planner."
                    : "Меню Alpha Map знайдено, але пункт 'Подключиться' відсутній.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Не вдалося запустити Alpha:\n" + ex.Message;
                return false;
            }
        }

        private static void OpenFlightData(PluginHost host)
        {
            var mainForm = host.MainForm;
            if (mainForm == null)
            {
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var menuFlightDataField = mainForm.GetType().GetField("MenuFlightData", flags);
            var menuFlightData = menuFlightDataField?.GetValue(mainForm) as ToolStripItem;
            menuFlightData?.PerformClick();
        }

        private static ToolStripMenuItem FindAlphaMenu(PluginHost host)
        {
            var candidates = new[] { host.FDMenuMap, host.FPMenuMap };
            foreach (var menu in candidates)
            {
                if (menu == null)
                    continue;

                var alphaItem = menu.Items
                    .OfType<ToolStripItem>()
                    .FirstOrDefault(item => string.Equals((item.Text ?? string.Empty).Trim(), "Alpha Map", StringComparison.OrdinalIgnoreCase));
                if (alphaItem is ToolStripMenuItem alphaMenu)
                {
                    return alphaMenu;
                }
            }

            return null;
        }
    }
}
