using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MissionWizardPlugin
{
    internal sealed class BombingTableSnapshot
    {
        public string SourceFile { get; set; }
        public float? AircraftSpeedMps { get; set; }
        public float? WindDirFromDeg { get; set; }
        public float? WindSpeedMps { get; set; }
        public float? TargetLat { get; set; }
        public float? TargetLon { get; set; }
        public float? DropHeightMeters { get; set; }
        public float? ReleaseDistanceMeters { get; set; }


        // D column = altitude (m), H column = відносна дистанція сброса (м)
        internal List<(float altM, float releaseM)> AltitudeTable { get; } = new List<(float, float)>();

        /// <summary>
        /// Повертає дистанцію сброса (відносно) з таблиці для заданої висоти з лінійною інтерполяцією.
        /// </summary>
        public float GetReleaseDistance(float altitudeMeters)
        {
            if (AltitudeTable.Count == 0) return 0f;
            var sorted = AltitudeTable.OrderBy(r => r.altM).ToList();
            if (altitudeMeters <= sorted[0].altM) return sorted[0].releaseM;
            if (altitudeMeters >= sorted[sorted.Count - 1].altM) return sorted[sorted.Count - 1].releaseM;
            for (var i = 0; i < sorted.Count - 1; i++)
            {
                if (altitudeMeters >= sorted[i].altM && altitudeMeters <= sorted[i + 1].altM)
                {
                    var t = (altitudeMeters - sorted[i].altM) / (sorted[i + 1].altM - sorted[i].altM);
                    return sorted[i].releaseM + t * (sorted[i + 1].releaseM - sorted[i].releaseM);
                }
            }
            return sorted[sorted.Count - 1].releaseM;
        }
    }

    internal static class BombingTableXlsx
    {
        public static bool TryLoad(string xlsxPath, out BombingTableSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(xlsxPath) || !File.Exists(xlsxPath))
            {
                error = "Файл таблиці не знайдено.";
                return false;
            }

            try
            {
                using (var archive = ZipFile.OpenRead(xlsxPath))
                {
                    var shared = ReadSharedStrings(archive);
                    var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheetEntry == null)
                    {
                        error = "У файлі не знайдено sheet1.xml";
                        return false;
                    }

                    var cells = ReadCells(sheetEntry, shared);
                    var data = new BombingTableSnapshot
                    {
                        SourceFile = xlsxPath,
                        AircraftSpeedMps = GetFloat(cells, "B5"),
                        WindDirFromDeg = GetFloat(cells, "B9"),
                        WindSpeedMps = GetFloat(cells, "B10"),
                    };

                    // D column = altitude (m), H column = відносна дистанція сброса
                    for (var row = 3; row <= 30; row++)
                    {
                        var altVal = GetFloat(cells, "D" + row);
                        var relVal = GetFloat(cells, "H" + row);
                        if (altVal.HasValue && relVal.HasValue && altVal.Value > 0 && relVal.Value >= 0)
                        {
                            data.AltitudeTable.Add((altVal.Value, relVal.Value));
                        }
                    }

                    snapshot = data;
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = "Помилка читання XLSX: " + ex.Message;
                return false;
            }
        }

        private static Dictionary<string, string> ReadCells(ZipArchiveEntry sheetEntry, IList<string> sharedStrings)
        {
            using (var stream = sheetEntry.Open())
            {
                var doc = XDocument.Load(stream);
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var c in doc.Descendants().Where(e => e.Name.LocalName == "c"))
                {
                    var cellRef = c.Attribute("r")?.Value;
                    if (string.IsNullOrEmpty(cellRef))
                    {
                        continue;
                    }

                    var cellType = c.Attribute("t")?.Value;
                    var valueElement = c.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
                    if (valueElement == null)
                    {
                        continue;
                    }

                    var raw = valueElement.Value;
                    if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                            && idx >= 0
                            && idx < sharedStrings.Count)
                        {
                            result[cellRef] = sharedStrings[idx];
                        }
                    }
                    else
                    {
                        result[cellRef] = raw;
                    }
                }

                return result;
            }
        }

        private static IList<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return Array.Empty<string>();
            }

            using (var stream = entry.Open())
            {
                var doc = XDocument.Load(stream);
                var list = new List<string>();

                foreach (var si in doc.Descendants().Where(e => e.Name.LocalName == "si"))
                {
                    var parts = si.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value);
                    list.Add(string.Concat(parts));
                }

                return list;
            }
        }

        private static double? GetDouble(Dictionary<string, string> cells, string cell)
        {
            if (!cells.TryGetValue(cell, out var text))
            {
                return null;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return null;
        }

        private static float? GetFloat(Dictionary<string, string> cells, string cell)
        {
            var value = GetDouble(cells, cell);
            if (!value.HasValue)
            {
                return null;
            }

            return (float)value.Value;
        }
    }
}

