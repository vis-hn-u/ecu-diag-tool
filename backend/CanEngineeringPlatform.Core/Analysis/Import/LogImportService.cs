using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Analysis.Import;

public class LogImportResult
{
    public bool Success { get; set; }
    public LogFileFormat DetectedFormat { get; set; }
    public string Message { get; set; } = string.Empty;
    public CanonicalLogDataSet? DataSet { get; set; }
    public LogSession? CanSession { get; set; }
}

public interface ILogImportService
{
    LogFileFormat DetectFormat(string content);
    LogImportResult ImportLog(string content, string fileName = "imported_log");
}

public class LogImportService : ILogImportService
{
    public LogFileFormat DetectFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return LogFileFormat.GenericTimeSeriesCsv;

        string firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        string firstFiveLines = string.Join("\n", content.Split('\n').Take(5));

        // 1. JSON
        if (firstLine.StartsWith("{") || firstLine.StartsWith("["))
        {
            return LogFileFormat.JsonLog;
        }

        // 2. candump: "(123456.789) can0 123#" or "can0 123 [8]"
        if (Regex.IsMatch(firstFiveLines, @"\(\d+\.\d+\)\s+can\d\s+[0-9a-fA-F]+#") ||
            Regex.IsMatch(firstFiveLines, @"can\d\s+[0-9a-fA-F]+\s+\[\d+\]"))
        {
            return LogFileFormat.LinuxCandump;
        }

        // 3. Vector ASC: "date ... base hex" or lines like "0.0012 1 123 Rx"
        if (firstFiveLines.Contains("base hex", StringComparison.OrdinalIgnoreCase) ||
            firstFiveLines.Contains("date ", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(firstFiveLines, @"^\s*\d+\.\d+\s+\d+\s+[0-9a-fA-F]+[xX]?\s+(Rx|Tx)", RegexOptions.Multiline))
        {
            return LogFileFormat.VectorAsc;
        }

        // 4. Torque Pro CSV: contains "GPS Time" or "Device Time"
        if (firstLine.Contains("GPS Time", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Contains("Device Time", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Contains("Speed (OBD)", StringComparison.OrdinalIgnoreCase))
        {
            return LogFileFormat.TorqueProCsv;
        }

        // 5. VCDS CSV: contains "Group" or "STAMP"
        if (firstFiveLines.Contains("Group", StringComparison.OrdinalIgnoreCase) &&
            firstFiveLines.Contains("STAMP", StringComparison.OrdinalIgnoreCase))
        {
            return LogFileFormat.VcdsCsv;
        }

        // 6. Generic CAN CSV: contains "TimestampUs" or "IdHex" or "DLC"
        if (firstLine.Contains("TimestampUs", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Contains("IdHex", StringComparison.OrdinalIgnoreCase))
        {
            return LogFileFormat.GenericCanCsv;
        }

        // 7. Fallback: Generic Time-Series CSV
        return LogFileFormat.GenericTimeSeriesCsv;
    }

    public LogImportResult ImportLog(string content, string fileName = "imported_log")
    {
        var format = DetectFormat(content);
        try
        {
            return format switch
            {
                LogFileFormat.GenericCanCsv => ImportGenericCanCsv(content, fileName),
                LogFileFormat.TorqueProCsv => ImportTorqueProCsv(content, fileName),
                LogFileFormat.VcdsCsv => ImportVcdsCsv(content, fileName),
                LogFileFormat.LinuxCandump => ImportCandump(content, fileName),
                LogFileFormat.VectorAsc => ImportVectorAsc(content, fileName),
                LogFileFormat.JsonLog => ImportJsonLog(content, fileName),
                _ => ImportGenericTimeSeriesCsv(content, fileName)
            };
        }
        catch (Exception ex)
        {
            return new LogImportResult
            {
                Success = false,
                DetectedFormat = format,
                Message = $"Failed to parse log as {format}: {ex.Message}"
            };
        }
    }

    // ==========================================
    // Format Importers
    // ==========================================
    private LogImportResult ImportGenericCanCsv(string content, string fileName)
    {
        var session = new LogSession { Name = fileName, StartTimeUtc = DateTime.UtcNow };
        var dataset = new CanonicalLogDataSet { Name = fileName, SourceFormat = LogFileFormat.GenericCanCsv };

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new LogImportResult { Success = false, Message = "CSV contains no data rows" };
        }

        var header = lines[0].Split(',').Select(h => h.Trim()).ToList();
        int timeIdx = header.FindIndex(h => h.Equals("TimestampUs", StringComparison.OrdinalIgnoreCase) || h.Equals("Time", StringComparison.OrdinalIgnoreCase) || h.Equals("Timestamp", StringComparison.OrdinalIgnoreCase));
        int idIdx = header.FindIndex(h => h.Equals("IdHex", StringComparison.OrdinalIgnoreCase) || h.Equals("IdDec", StringComparison.OrdinalIgnoreCase) || h.Equals("Id", StringComparison.OrdinalIgnoreCase) || h.Equals("CanId", StringComparison.OrdinalIgnoreCase));
        int dataIdx = header.FindIndex(h => h.Equals("DataHex", StringComparison.OrdinalIgnoreCase) || h.Equals("Data", StringComparison.OrdinalIgnoreCase) || h.Equals("Payload", StringComparison.OrdinalIgnoreCase));
        int chIdx = header.FindIndex(h => h.Equals("Channel", StringComparison.OrdinalIgnoreCase) || h.Equals("Ch", StringComparison.OrdinalIgnoreCase));
        int dlcIdx = header.FindIndex(h => h.Equals("DLC", StringComparison.OrdinalIgnoreCase));

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length <= Math.Max(timeIdx, Math.Max(idIdx, dataIdx))) continue;

            ulong.TryParse(parts[timeIdx].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong tsUsRaw);
            uint tsUs = (uint)(tsUsRaw % uint.MaxValue);

            string idStr = parts[idIdx].Trim();
            uint id = 0;
            if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                id = Convert.ToUInt32(idStr.Substring(2), 16);
            }
            else if (uint.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint decId))
            {
                id = decId;
            }
            else
            {
                id = Convert.ToUInt32(idStr, 16);
            }

            byte channel = (chIdx >= 0 && chIdx < parts.Length) ? byte.Parse(parts[chIdx].Trim()) : (byte)0;
            byte dlc = (dlcIdx >= 0 && dlcIdx < parts.Length) ? byte.Parse(parts[dlcIdx].Trim()) : (byte)8;

            string dataStr = parts[dataIdx].Trim().Replace(" ", "");
            byte[] data = ParseHexBytes(dataStr);

            var frame = new CanFrame(tsUs, id, dlc, 0, data, channel: channel);
            session.Frames.Add(frame);
        }

        return new LogImportResult
        {
            Success = true,
            DetectedFormat = LogFileFormat.GenericCanCsv,
            CanSession = session,
            DataSet = dataset,
            Message = $"Successfully imported {session.Frames.Count} CAN frames from CSV."
        };
    }

    private LogImportResult ImportTorqueProCsv(string content, string fileName)
    {
        var dataset = new CanonicalLogDataSet
        {
            Name = fileName,
            SourceFormat = LogFileFormat.TorqueProCsv,
            StartTimeUtc = DateTime.UtcNow
        };

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new LogImportResult { Success = false, Message = "Torque CSV contains no rows." };
        }

        var header = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToList();
        int latIdx = header.FindIndex(h => h.Contains("Latitude", StringComparison.OrdinalIgnoreCase));
        int lonIdx = header.FindIndex(h => h.Contains("Longitude", StringComparison.OrdinalIgnoreCase));
        int altIdx = header.FindIndex(h => h.Contains("Altitude", StringComparison.OrdinalIgnoreCase));
        int timeIdx = header.FindIndex(h => h.Contains("Time", StringComparison.OrdinalIgnoreCase));

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',').Select(p => p.Trim().Trim('"')).ToArray();
            if (parts.Length < header.Count) continue;

            double timeMs = (i - 1) * 100.0; // Default 10 Hz if time parse fails
            if (timeIdx >= 0 && DateTime.TryParse(parts[timeIdx], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                if (i == 1) dataset.StartTimeUtc = dt;
                timeMs = Math.Max(0.0, (dt - dataset.StartTimeUtc).TotalMilliseconds);
            }

            // Parse GPS Point
            if (latIdx >= 0 && lonIdx >= 0 &&
                double.TryParse(parts[latIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(parts[lonIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon) &&
                (lat != 0.0 || lon != 0.0))
            {
                double? alt = (altIdx >= 0 && double.TryParse(parts[altIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double a)) ? a : null;
                dataset.GpsTrack.Add(new GpsPoint
                {
                    TimestampMs = timeMs,
                    Latitude = lat,
                    Longitude = lon,
                    AltitudeMeters = alt
                });
            }

            // Parse all numeric sensor columns
            for (int col = 0; col < header.Count; col++)
            {
                if (col == timeIdx || col == latIdx || col == lonIdx || col == altIdx) continue;
                string colName = CleanChannelName(header[col]);
                if (double.TryParse(parts[col], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    dataset.AddChannelPoint(colName, timeMs, val, source: "OBD");
                }
            }
        }

        GpsSyncEngine.SynchronizeGpsWithCan(dataset);

        return new LogImportResult
        {
            Success = true,
            DetectedFormat = LogFileFormat.TorqueProCsv,
            DataSet = dataset,
            Message = $"Imported Torque CSV with {dataset.Channels.Count} channels and {dataset.GpsTrack.Count} GPS points."
        };
    }

    private LogImportResult ImportVcdsCsv(string content, string fileName)
    {
        var dataset = new CanonicalLogDataSet
        {
            Name = fileName,
            SourceFormat = LogFileFormat.VcdsCsv,
            StartTimeUtc = DateTime.UtcNow
        };

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        // Find row where headers appear (usually row 4 or 5 after Group markers)
        int headerRowIdx = -1;
        for (int i = 0; i < Math.Min(10, lines.Length); i++)
        {
            if (lines[i].Contains("STAMP", StringComparison.OrdinalIgnoreCase) || lines[i].Contains("Marker", StringComparison.OrdinalIgnoreCase))
            {
                headerRowIdx = i;
                break;
            }
        }

        if (headerRowIdx < 0 || headerRowIdx >= lines.Length - 1)
        {
            return ImportGenericTimeSeriesCsv(content, fileName);
        }

        var headerCols = lines[headerRowIdx].Split(',').Select(h => h.Trim().Trim('"')).ToList();
        // Check if next row contains units
        var unitCols = (headerRowIdx + 1 < lines.Length)
            ? lines[headerRowIdx + 1].Split(',').Select(u => u.Trim().Trim('"')).ToList()
            : new List<string>();

        int dataStartRow = headerRowIdx + (unitCols.Count > 0 ? 2 : 1);

        for (int r = dataStartRow; r < lines.Length; r++)
        {
            var parts = lines[r].Split(',').Select(p => p.Trim().Trim('"')).ToArray();
            if (parts.Length < 2) continue;

            double timeMs = 0.0;
            // First STAMP column is timestamp
            int stampIdx = headerCols.FindIndex(h => h.Equals("STAMP", StringComparison.OrdinalIgnoreCase));
            if (stampIdx >= 0 && stampIdx < parts.Length && double.TryParse(parts[stampIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double stampSec))
            {
                timeMs = stampSec * 1000.0;
            }
            else
            {
                timeMs = (r - dataStartRow) * 100.0;
            }

            for (int col = 0; col < headerCols.Count && col < parts.Length; col++)
            {
                if (col == stampIdx || headerCols[col].Equals("STAMP", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(headerCols[col])) continue;
                string chanName = CleanChannelName(headerCols[col]);
                string units = col < unitCols.Count ? unitCols[col] : "";

                if (double.TryParse(parts[col], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    dataset.AddChannelPoint(chanName, timeMs, val, units: units, source: "VCDS");
                }
            }
        }

        return new LogImportResult
        {
            Success = true,
            DetectedFormat = LogFileFormat.VcdsCsv,
            DataSet = dataset,
            Message = $"Imported VCDS log with {dataset.Channels.Count} channels."
        };
    }

    private LogImportResult ImportCandump(string content, string fileName)
    {
        var session = new LogSession { Name = fileName, StartTimeUtc = DateTime.UtcNow };
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Pattern 1: (1600000000.123456) can0 123#11223344
        // Pattern 2: can0 123 [8] 11 22 33 44 55 66 77 88
        var regex1 = new Regex(@"^\s*\((?<ts>\d+\.\d+)\)\s+(?<ch>\w+)\s+(?<id>[0-9a-fA-F]+)#(?<data>[0-9a-fA-F]*)");
        var regex2 = new Regex(@"^\s*(?<ch>\w+)\s+(?<id>[0-9a-fA-F]+)\s+\[(?<dlc>\d+)\]\s+(?<data>[0-9a-fA-F ]+)");

        foreach (var line in lines)
        {
            var m1 = regex1.Match(line);
            if (m1.Success)
            {
                double sec = double.Parse(m1.Groups["ts"].Value, CultureInfo.InvariantCulture);
                uint tsUs = (uint)((sec * 1_000_000.0) % uint.MaxValue);
                uint id = Convert.ToUInt32(m1.Groups["id"].Value, 16);
                byte[] data = ParseHexBytes(m1.Groups["data"].Value);
                byte ch = (byte)(m1.Groups["ch"].Value.EndsWith("1") ? 1 : 0);

                session.Frames.Add(new CanFrame(tsUs, id, (byte)data.Length, 0, data, channel: ch));
                continue;
            }

            var m2 = regex2.Match(line);
            if (m2.Success)
            {
                uint id = Convert.ToUInt32(m2.Groups["id"].Value, 16);
                byte dlc = byte.Parse(m2.Groups["dlc"].Value);
                byte[] data = ParseHexBytes(m2.Groups["data"].Value.Replace(" ", ""));
                byte ch = (byte)(m2.Groups["ch"].Value.EndsWith("1") ? 1 : 0);
                uint tsUs = (uint)((session.Frames.Count * 1000) % uint.MaxValue); // monotonic fallback

                session.Frames.Add(new CanFrame(tsUs, id, dlc, 0, data, channel: ch));
            }
        }

        return new LogImportResult
        {
            Success = session.Frames.Count > 0,
            DetectedFormat = LogFileFormat.LinuxCandump,
            CanSession = session,
            Message = $"Parsed {session.Frames.Count} frames from candump text log."
        };
    }

    private LogImportResult ImportVectorAsc(string content, string fileName)
    {
        var session = new LogSession { Name = fileName, StartTimeUtc = DateTime.UtcNow };
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Vector format line: 0.0012 1 123x Rx d 8 00 11 22 33 44 55 66 77
        var ascRegex = new Regex(@"^\s*(?<ts>\d+\.\d+)\s+(?<ch>\d+)\s+(?<id>[0-9a-fA-F]+)(?<ext>[xX])?\s+(?<dir>Rx|Tx)\s+d\s+(?<dlc>\d+)\s+(?<data>[0-9a-fA-F ]+)");

        foreach (var line in lines)
        {
            var m = ascRegex.Match(line);
            if (m.Success)
            {
                double sec = double.Parse(m.Groups["ts"].Value, CultureInfo.InvariantCulture);
                uint tsUs = (uint)((sec * 1_000_000.0) % uint.MaxValue);
                uint id = Convert.ToUInt32(m.Groups["id"].Value, 16);
                bool isExt = m.Groups["ext"].Success;
                byte dlc = byte.Parse(m.Groups["dlc"].Value);
                byte[] data = ParseHexBytes(m.Groups["data"].Value.Replace(" ", ""));
                byte ch = (byte)(byte.Parse(m.Groups["ch"].Value) - 1);

                byte flags = isExt ? CanFrame.FlagExtended : (byte)0;
                session.Frames.Add(new CanFrame(tsUs, id, dlc, flags, data, channel: ch));
            }
        }

        return new LogImportResult
        {
            Success = session.Frames.Count > 0,
            DetectedFormat = LogFileFormat.VectorAsc,
            CanSession = session,
            Message = $"Parsed {session.Frames.Count} frames from Vector ASC log."
        };
    }

    private LogImportResult ImportGenericTimeSeriesCsv(string content, string fileName)
    {
        var dataset = new CanonicalLogDataSet
        {
            Name = fileName,
            SourceFormat = LogFileFormat.GenericTimeSeriesCsv,
            StartTimeUtc = DateTime.UtcNow
        };

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return new LogImportResult { Success = false, Message = "CSV contains no data rows." };
        }

        var header = lines[0].Split(',').Select(h => CleanChannelName(h)).ToList();
        int timeIdx = header.FindIndex(h => h.StartsWith("time", StringComparison.OrdinalIgnoreCase) || h.StartsWith("sec", StringComparison.OrdinalIgnoreCase) || h.StartsWith("stamp", StringComparison.OrdinalIgnoreCase));

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < header.Count) continue;

            double timeMs = (timeIdx >= 0 && double.TryParse(parts[timeIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double t))
                ? t * 1000.0
                : (i - 1) * 20.0;

            for (int col = 0; col < header.Count; col++)
            {
                if (col == timeIdx) continue;
                if (double.TryParse(parts[col].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    dataset.AddChannelPoint(header[col], timeMs, val, source: "CSV");
                }
            }
        }

        return new LogImportResult
        {
            Success = dataset.Channels.Count > 0,
            DetectedFormat = LogFileFormat.GenericTimeSeriesCsv,
            DataSet = dataset,
            Message = $"Imported {dataset.Channels.Count} channels from generic time-series CSV."
        };
    }

    private LogImportResult ImportJsonLog(string content, string fileName)
    {
        try
        {
            var dataset = JsonSerializer.Deserialize<CanonicalLogDataSet>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dataset != null && dataset.Channels.Count > 0)
            {
                return new LogImportResult
                {
                    Success = true,
                    DetectedFormat = LogFileFormat.JsonLog,
                    DataSet = dataset,
                    Message = $"Successfully deserialized JSON time-series log with {dataset.Channels.Count} channels."
                };
            }
        }
        catch { }

        return new LogImportResult
        {
            Success = false,
            DetectedFormat = LogFileFormat.JsonLog,
            Message = "Failed to deserialize JSON into CanonicalLogDataSet"
        };
    }

    // ==========================================
    // Helpers
    // ==========================================
    private static byte[] ParseHexBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
        if (hex.Length % 2 != 0) hex = "0" + hex;

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private static string CleanChannelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unknown";
        // Strip trailing units in parentheses or brackets e.g. "Engine RPM(rpm)" -> "Engine RPM", "Boost(bar)" -> "Boost"
        string cleaned = Regex.Replace(name.Trim().Trim('"'), @"\s*[\(\[][a-zA-Z0-9/%_\.\- ]+[\)\]]\s*$", "").Trim();
        return string.IsNullOrEmpty(cleaned) ? name.Trim().Trim('"') : cleaned;
    }
}
