using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal static class AutoMissionService
    {
        private static readonly object XlsxCacheLock = new object();
        private static string cachedXlsxPath;
        private static DateTime cachedXlsxWriteUtc = DateTime.MinValue;
        private static BombingTableSnapshot cachedXlsx;

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
            ExecuteInternal(host, targetLat, targetLon, runAsync: false);
        }

        public static void ExecuteAsync(PluginHost host, double targetLat, double targetLon)
        {
            ExecuteInternal(host, targetLat, targetLon, runAsync: true);
        }

        private static void ExecuteInternal(PluginHost host, double targetLat, double targetLon, bool runAsync)
        {
            var uiTarget = host?.MainForm as Control;

            Action beginBusy = () =>
            {
                if (uiTarget != null && !uiTarget.IsDisposed)
                {
                    uiTarget.Cursor = Cursors.WaitCursor;
                }
            };

            Action endBusy = () =>
            {
                if (uiTarget != null && !uiTarget.IsDisposed)
                {
                    uiTarget.Cursor = Cursors.Default;
                }
            };

            Action<Action> invokeUi = action =>
            {
                if (uiTarget != null && !uiTarget.IsDisposed)
                {
                    uiTarget.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            };

            invokeUi(beginBusy);

            Action work = () =>
            {
                try
                {
                    var result = BuildMissionPayload(host, targetLat, targetLon);

                    invokeUi(() =>
                    {
                        try
                        {
                            MissionPlannerIntegration.LoadWaypointsIntoFlightPlanner(host, result.FilePath);

                            MessageBox.Show(
                                $"Місію побудовано автоматично.\n\n" +
                                $"Ціль:         {targetLat:F6}, {targetLon:F6}\n" +
                                $"Висота:       {result.Input.DropHeightAboveTargetMeters:F0} м\n" +
                                $"{result.RelOffsetText}\n" +
                                $"Вітер:        {result.Input.WindSpeedMps:F1} м/с з {result.Input.WindDirectionFromDeg:F0}° ({result.Input.WindSource})\n" +
                                $"Посадка:      коробка, глісада {result.Input.LandingGlideSlopeDeg:F1}°\n" +
                                $"Швидкість:    {result.Input.SpeedMetersPerSecond:F0} м/с\n" +
                                $"Команд у місії: {result.MissionCount}\n\n" +
                                "Місію завантажено у Flight Plan.",
                                "Автоматична місія",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        finally
                        {
                            endBusy();
                        }
                    });
                }
                catch (Exception ex)
                {
                    invokeUi(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                "Помилка побудови місії:\n" + ex.Message,
                                "Автоматична місія",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                        finally
                        {
                            endBusy();
                        }
                    });
                }
            };

            if (runAsync)
            {
                Task.Run(work);
            }
            else
            {
                work();
            }
        }

        private static MissionBuildPayload BuildMissionPayload(PluginHost host, double targetLat, double targetLon)
        {
            try
            {
                var input = new MissionWizardInput
                {
                    DeliveryTargetLat = targetLat,
                    DeliveryTargetLon = targetLon,
                    DeliveryRunInMeters = 1000,
                    UseDeliveryTarget = true,
                    HasDeliveryPoint = true,
                    DeliveryOnlyMission = true,
                    UsePointRoute = true,
                    AddPayloadRelease = true
                };

                // 1. Defaults from autopilot and weather
                MissionContextResolver.ApplyDefaults(host, input, resolveExternal: true);

                // 2. Override with bombing table if available
                BombingTableSnapshot table = null;
                TryGetCachedBombingTable(out table);

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

                // 2.1 Auto-landing setup: use landing point from map, otherwise return to HOME via landing box.
                if (MissionPointsStore.HasLanding)
                {
                    input.LandingLat = MissionPointsStore.LandingLat;
                    input.LandingLon = MissionPointsStore.LandingLon;
                }
                else
                {
                    input.LandingLat = input.HomeLat;
                    input.LandingLon = input.HomeLon;
                }

                input.HasLandingPoint = true;
                input.LandingRunInMeters = Math.Max(input.LandingRunInMeters, 1000f);
                input.LandingGlideSlopeDeg = 1.8f;

                // 3. Build and load mission
                var mission = MissionBuilder.Build(input);
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MissionWizard");
                var filePath = MissionBuilder.WriteQgcWpl(mission, outputDir);

                // 4. Confirmation
                var relOffset = input.BombReleaseOffsetMeters > 0
                    ? $"Відносна: {input.BombReleaseOffsetMeters:F0} м"
                    : "Пряме наведення (без відносу)";

                return new MissionBuildPayload
                {
                    Input = input,
                    MissionCount = mission.Count,
                    FilePath = filePath,
                    RelOffsetText = relOffset
                };
            }
            catch
            {
                throw;
            }
        }

        private static bool TryGetCachedBombingTable(out BombingTableSnapshot snapshot)
        {
            snapshot = null;
            var xlsxPath = XlsxSearchPaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(xlsxPath))
            {
                return false;
            }

            var writeUtc = File.GetLastWriteTimeUtc(xlsxPath);

            lock (XlsxCacheLock)
            {
                if (cachedXlsx != null &&
                    string.Equals(cachedXlsxPath, xlsxPath, StringComparison.OrdinalIgnoreCase) &&
                    cachedXlsxWriteUtc == writeUtc)
                {
                    snapshot = cachedXlsx;
                    return true;
                }

                if (BombingTableXlsx.TryLoad(xlsxPath, out var loaded, out _))
                {
                    cachedXlsxPath = xlsxPath;
                    cachedXlsxWriteUtc = writeUtc;
                    cachedXlsx = loaded;
                    snapshot = cachedXlsx;
                    return true;
                }
            }

            return false;
        }

        private sealed class MissionBuildPayload
        {
            public MissionWizardInput Input { get; set; }
            public int MissionCount { get; set; }
            public string FilePath { get; set; }
            public string RelOffsetText { get; set; }
        }
    }
}
