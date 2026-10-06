using System;
using System.Collections.Generic;
using System.Text;

namespace CanEngineeringPlatform.Core.Diagnostics.Obd;

public static class ObdDecoder
{
    private static readonly Dictionary<byte, ObdPidDefinition> StandardPids = new();

    static ObdDecoder()
    {
        // Mode 01 Standard PIDs
        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x04,
            Name = "CalculatedEngineLoad",
            Description = "Calculated Engine Load",
            ReturnBytes = 1,
            Unit = "%",
            MinValue = 0,
            MaxValue = 100,
            Decoder = d => Math.Round(d[0] * 100.0 / 255.0, 1),
            Formatter = d => $"{d[0] * 100.0 / 255.0:F1} %"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x05,
            Name = "EngineCoolantTemp",
            Description = "Engine Coolant Temperature",
            ReturnBytes = 1,
            Unit = "°C",
            MinValue = -40,
            MaxValue = 215,
            Decoder = d => d[0] - 40.0,
            Formatter = d => $"{d[0] - 40} °C"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x0C,
            Name = "EngineSpeed",
            Description = "Engine RPM",
            ReturnBytes = 2,
            Unit = "rpm",
            MinValue = 0,
            MaxValue = 16383.75,
            Decoder = d => Math.Round(((d[0] << 8) | d[1]) / 4.0, 1),
            Formatter = d => $"{((d[0] << 8) | d[1]) / 4.0:F1} rpm"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x0D,
            Name = "VehicleSpeed",
            Description = "Vehicle Speed",
            ReturnBytes = 1,
            Unit = "km/h",
            MinValue = 0,
            MaxValue = 255,
            Decoder = d => d[0],
            Formatter = d => $"{d[0]} km/h"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x0F,
            Name = "IntakeAirTemp",
            Description = "Intake Air Temperature",
            ReturnBytes = 1,
            Unit = "°C",
            MinValue = -40,
            MaxValue = 215,
            Decoder = d => d[0] - 40.0,
            Formatter = d => $"{d[0] - 40} °C"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x10,
            Name = "MafAirFlowRate",
            Description = "MAF Air Flow Rate",
            ReturnBytes = 2,
            Unit = "g/s",
            MinValue = 0,
            MaxValue = 655.35,
            Decoder = d => Math.Round(((d[0] << 8) | d[1]) / 100.0, 2),
            Formatter = d => $"{((d[0] << 8) | d[1]) / 100.0:F2} g/s"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x11,
            Name = "ThrottlePosition",
            Description = "Throttle Position",
            ReturnBytes = 1,
            Unit = "%",
            MinValue = 0,
            MaxValue = 100,
            Decoder = d => Math.Round(d[0] * 100.0 / 255.0, 1),
            Formatter = d => $"{d[0] * 100.0 / 255.0:F1} %"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x1F,
            Name = "RunTimeSinceEngineStart",
            Description = "Run Time Since Engine Start",
            ReturnBytes = 2,
            Unit = "sec",
            MinValue = 0,
            MaxValue = 65535,
            Decoder = d => (d[0] << 8) | d[1],
            Formatter = d => $"{(d[0] << 8) | d[1]} sec"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x2F,
            Name = "FuelTankLevelInput",
            Description = "Fuel Tank Level Input",
            ReturnBytes = 1,
            Unit = "%",
            MinValue = 0,
            MaxValue = 100,
            Decoder = d => Math.Round(d[0] * 100.0 / 255.0, 1),
            Formatter = d => $"{d[0] * 100.0 / 255.0:F1} %"
        });

        RegisterPid(new ObdPidDefinition
        {
            Pid = 0x42,
            Name = "ControlModuleVoltage",
            Description = "Control Module Voltage",
            ReturnBytes = 2,
            Unit = "V",
            MinValue = 0,
            MaxValue = 65.535,
            Decoder = d => Math.Round(((d[0] << 8) | d[1]) / 1000.0, 2),
            Formatter = d => $"{((d[0] << 8) | d[1]) / 1000.0:F2} V"
        });
    }

    private static void RegisterPid(ObdPidDefinition pid)
    {
        StandardPids[pid.Pid] = pid;
    }

    public static ObdPidDefinition? GetPidDefinition(byte pid)
    {
        StandardPids.TryGetValue(pid, out var def);
        return def;
    }

    public static IReadOnlyCollection<ObdPidDefinition> GetAllPidDefinitions() => StandardPids.Values;

    public static ObdResponse ParseResponse(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return new ObdResponse { IsPositive = false, DecodedSummary = "Empty OBD response" };
        }

        byte sid = payload[0];

        // Positive response SID = Mode + 0x40 (0x41, 0x42, 0x43, 0x44, 0x49, etc.)
        if (sid < 0x41 || sid > 0x4A)
        {
            return new ObdResponse
            {
                IsPositive = false,
                RawData = payload,
                DecodedSummary = $"Unrecognized OBD Response SID (0x{sid:X2})"
            };
        }

        var mode = (ObdMode)(sid - 0x40);
        var response = new ObdResponse
        {
            IsPositive = true,
            Mode = mode,
            RawData = payload
        };

        switch (mode)
        {
            case ObdMode.CurrentData:
            case ObdMode.FreezeFrame:
                ParseMode01or02(payload, response);
                break;

            case ObdMode.StoredDTCs:
            case ObdMode.PendingDTCs:
                ParseMode03or07(payload, response);
                break;

            case ObdMode.ClearDTCs:
                response.DecodedSummary = "OBD DTCs Cleared Successfully (Mode 04)";
                break;

            case ObdMode.VehicleInformation:
                ParseMode09(payload, response);
                break;

            default:
                response.DecodedSummary = $"OBD Mode {mode} Response ({payload.Length} bytes)";
                break;
        }

        return response;
    }

    private static void ParseMode01or02(byte[] payload, ObdResponse response)
    {
        if (payload.Length < 2) return;

        byte pid = payload[1];
        response.Pid = pid;
        byte[] data = payload.Length > 2 ? payload[2..] : Array.Empty<byte>();

        // Check if this is a supported PID bitmap (PID 0x00, 0x20, 0x40, 0x60, 0x80, etc.)
        if (pid % 0x20 == 0 && data.Length >= 4)
        {
            uint bitmap = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
            response.DecodedSummary = $"Supported PIDs 0x{pid + 1:X2} - 0x{pid + 32:X2}: Bitmap 0x{bitmap:X8}";
            response.PidValues.Add(new ObdPidValue
            {
                Pid = pid,
                Name = $"SupportedPids_{pid:X2}",
                Value = bitmap,
                FormattedValue = $"0x{bitmap:X8}",
                Unit = "bitmap",
                RawBytes = data[0..4],
                IsSupported = true
            });
            return;
        }

        if (StandardPids.TryGetValue(pid, out var def))
        {
            double val = def.Decoder != null && data.Length >= def.ReturnBytes ? def.Decoder(data) : 0;
            string formatted = def.Formatter != null && data.Length >= def.ReturnBytes ? def.Formatter(data) : $"{val} {def.Unit}";

            response.PidValues.Add(new ObdPidValue
            {
                Pid = pid,
                Name = def.Name,
                Value = val,
                FormattedValue = formatted,
                Unit = def.Unit,
                RawBytes = data,
                IsSupported = true
            });
            response.DecodedSummary = $"{def.Name}: {formatted}";
        }
        else
        {
            response.PidValues.Add(new ObdPidValue
            {
                Pid = pid,
                Name = $"PID_0x{pid:X2}",
                FormattedValue = BitConverter.ToString(data).Replace("-", " "),
                Unit = "raw",
                RawBytes = data,
                IsSupported = true
            });
            response.DecodedSummary = $"PID 0x{pid:X2}: {BitConverter.ToString(data)}";
        }
    }

    private static void ParseMode03or07(byte[] payload, ObdResponse response)
    {
        // Format: 0x43 <DTC1_MSB> <DTC1_LSB> <DTC2_MSB> <DTC2_LSB> ...
        if (payload.Length < 2)
        {
            response.DecodedSummary = "No DTCs reported";
            return;
        }

        int offset = 1;
        while (offset + 1 < payload.Length)
        {
            byte b1 = payload[offset];
            byte b2 = payload[offset + 1];
            offset += 2;

            if (b1 == 0 && b2 == 0) continue; // Zero padding

            string dtc = DecodeStandardDtc(b1, b2);
            response.DtcCodes.Add(dtc);
        }

        response.DecodedSummary = response.DtcCodes.Count > 0
            ? $"DTCs ({response.DtcCodes.Count}): {string.Join(", ", response.DtcCodes)}"
            : "No DTCs reported (0 stored codes)";
    }

    private static void ParseMode09(byte[] payload, ObdResponse response)
    {
        if (payload.Length < 2) return;
        byte infotype = payload[1];
        byte[] data = payload.Length > 2 ? payload[2..] : Array.Empty<byte>();

        if (infotype == 0x02) // VIN
        {
            // First byte in data might be count of items or ASCII string
            string vin = Encoding.ASCII.GetString(data).Trim('\0', ' ');
            response.VehicleInfoValue = vin;
            response.DecodedSummary = $"Vehicle VIN: {vin}";
        }
        else
        {
            response.DecodedSummary = $"Mode 09 InfoType 0x{infotype:X2} Data ({data.Length} bytes)";
        }
    }

    public static string DecodeStandardDtc(byte highByte, byte lowByte)
    {
        char prefix = ((highByte >> 6) & 0x03) switch
        {
            0 => 'P', // Powertrain
            1 => 'C', // Chassis
            2 => 'B', // Body
            3 => 'U', // Network
            _ => 'P'
        };

        int d1 = (highByte >> 4) & 0x03;
        int d2 = highByte & 0x0F;
        int d3 = (lowByte >> 4) & 0x0F;
        int d4 = lowByte & 0x0F;

        return $"{prefix}{d1:X1}{d2:X1}{d3:X1}{d4:X1}";
    }

    public static HashSet<byte> DecodeSupportedPidsBitmap(byte basePid, uint bitmap)
    {
        var supported = new HashSet<byte>();
        for (int i = 0; i < 32; i++)
        {
            if (((bitmap >> (31 - i)) & 1) == 1)
            {
                supported.Add((byte)(basePid + i + 1));
            }
        }
        return supported;
    }
}
