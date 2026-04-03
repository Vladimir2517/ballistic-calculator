using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal static class AutoMissionService
    {
        private static readonly string[] XlsxSearchPaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "Таблица_бомбометания.xlsx"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Таблица_бомбометания.xlsx"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MissionWizard", "Таблица_бомбометания.xlsx"),
            @"C:\Projects\ballistic-calculator\input\Таблица_бомбометания.xlsx"
        };

        /// <summary>
        /// Будує та завантажує місію автоматично за однією точкою цілі.
        /// Всі параметри беруться з автопілоту, погоди та таблиці бомбометання.
        /// </summary>
        public static void Execute(PluginHost host, double targetLat, double targetLon)
        {
            try
            {
                var input = new MissionWizardInput
                {
                    DeliveryTargetLat = targetLat,
                    DeliveryTargetLon = targetLon,
                    UseDeliveryTarget = true,
                    HasDeliveryPoint = true,
                    DeliveryOnlyMission = true,
                    UsePointRoute = false,
                    AddPayloadRelease = true
                };

                // 1. Defaults from autopilot and weather
                MissionContextResolver.ApplyDefaults(host, input, resolveExternal: true);

                // 2. Override with bombing table if available
                var xlsxPath = XlsxSearchPaths.FirstOrDefault(File.Exists);
                BombingTableSnapshot table = null;
                if (xlsxPath != null && BombingTableXlsx.TryLoad(xlsxPath, out var loaded, out _))
                {
                    table = loaded;
                }

                if (table != null)
                {
                    if (table.AircraftSpeedMps.HasValue && table.AircraftSpeedMps.Value > 0)
                    {
                        input.SpeedMetersPerSecond = table.AircraftSpeedMps.Value;
                    }

                    if (table.WindDirFromDeg.HasValue)
                    {
                        input.WindDirectionFromDeg = table.WindDirFromDeg.Value;
                        input.WindSource = "XLSX";
                    }

                    if (table.WindSpeedMps.HasValue && table.WindSpeedMps.Value >= 0)
                    {
                        input.WindSpeedMps = table.WindSpeedMps.Value;
                    }

                    var dropAlt = input.DropHeightAboveTargetMeters > 0
                        ? input.DropHeightAboveTargetMeters
                        : 100f;

                    var releaseOffset = table.GetReleaseDistance(dropAlt);
                    if (releaseOffset > 0)
                    {
                        input.BombReleaseOffsetMeters = releaseOffset;
                    }
                }

                // 3. Build and load mission
                var mission = MissionBuilder.Build(input);
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MissionWizard");
                var filePath = MissionBuilder.WriteQgcWpl(mission, outputDir);
                MissionPlannerIntegration.LoadWaypointsIntoFlightPlanner(host, filePath);

                // 4. Confirmation
                var relOffset = input.BombReleaseOffsetMeters > 0
                    ? $"Відносна: {input.BombReleaseOffsetMeters:F0} м"
                    : "Пряме наведення (без відносу)";

                MessageBox.Show(
                    $"Місію побудовано автоматично.\n\n" +
                    $"Ціль:         {targetLat:F6}, {targetLon:F6}\n" +
                    $"Висота:       {input.DropHeightAboveTargetMeters:F0} м\n" +
                    $"{relOffset}\n" +
                    $"Вітер:        {input.WindSpeedMps:F1} м/с з {input.WindDirectionFromDeg:F0}° ({input.WindSource})\n" +
                    $"Швидкість:    {input.SpeedMetersPerSecond:F0} м/с\n" +
                    $"Команд у місії: {mission.Count}\n\n" +
                    "Місію завантажено у Flight Plan.",
                    "Автоматична місія",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Помилка побудови місії:\n" + ex.Message,
                    "Автоматична місія",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
