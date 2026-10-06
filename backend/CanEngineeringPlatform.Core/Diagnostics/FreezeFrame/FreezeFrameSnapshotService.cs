using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;

namespace CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;

public interface IFreezeFrameSnapshotService
{
    IReadOnlyList<DiagnosticFreezeFrame> GetAllSnapshots();
    DiagnosticFreezeFrame? GetSnapshotById(string id);
    DiagnosticFreezeFrame? GetSnapshotForDtc(string dtcCode);
    void StoreSnapshot(DiagnosticFreezeFrame snapshot);
    DiagnosticFreezeFrame DecodeUdsSnapshot(byte[] payload, string sourceEcu = "ECM");
    DiagnosticFreezeFrame DecodeObdMode02(byte[] payload, string sourceEcu = "ECM");
    DiagnosticFreezeFrame GenerateSimulatedFreezeFrame(string dtcCode, string sourceEcu = "ECM");
}

public class FreezeFrameSnapshotService : IFreezeFrameSnapshotService
{
    private readonly ConcurrentDictionary<string, DiagnosticFreezeFrame> _snapshots = new();

    public FreezeFrameSnapshotService()
    {
        // Seed standard initial freeze frames for common diagnostic trouble codes
        StoreSnapshot(GenerateSimulatedFreezeFrame("P0300", "ECM"));
        StoreSnapshot(GenerateSimulatedFreezeFrame("P0171", "ECM"));
        StoreSnapshot(GenerateSimulatedFreezeFrame("P0087", "ECM"));
    }

    public IReadOnlyList<DiagnosticFreezeFrame> GetAllSnapshots() =>
        _snapshots.Values.OrderByDescending(s => s.TimestampUtc).ToList();

    public DiagnosticFreezeFrame? GetSnapshotById(string id)
    {
        _snapshots.TryGetValue(id, out var snapshot);
        return snapshot;
    }

    public DiagnosticFreezeFrame? GetSnapshotForDtc(string dtcCode)
    {
        return _snapshots.Values.FirstOrDefault(s => string.Equals(s.DtcCode, dtcCode, StringComparison.OrdinalIgnoreCase));
    }

    public void StoreSnapshot(DiagnosticFreezeFrame snapshot)
    {
        if (snapshot == null) return;
        _snapshots[snapshot.Id] = snapshot;
    }

    /// <summary>
    /// Decodes UDS Service 0x19 0x04 DTC Snapshot Record:
    /// Response: 0x59 0x04 [DTC_MSB DTC_MID DTC_LSB] [Status] [RecordNum] [NumberOfIdentifiers] [DID_MSB DID_LSB Data...]
    /// </summary>
    public DiagnosticFreezeFrame DecodeUdsSnapshot(byte[] payload, string sourceEcu = "ECM")
    {
        if (payload == null || payload.Length < 6 || payload[0] != 0x59)
        {
            throw new ArgumentException("Invalid UDS 0x19 0x04 snapshot response payload", nameof(payload));
        }

        string dtcCode = DtcDecoder.Decode3ByteDtc(payload[2], payload[3], payload[4]);
        byte status = payload.Length > 5 ? payload[5] : (byte)0x08;
        byte recNum = payload.Length > 6 ? payload[6] : (byte)0x00;

        var snapshot = new DiagnosticFreezeFrame
        {
            DtcCode = dtcCode,
            RecordNumber = recNum,
            SourceEcu = sourceEcu,
            Protocol = "UDS 0x19 04",
            StatusByte = status,
            TimestampUtc = DateTime.UtcNow,
            FaultTriggerReason = $"UDS DTC {dtcCode} Triggered"
        };

        int offset = 8; // Offset after NumberOfIdentifiers
        while (offset + 2 <= payload.Length)
        {
            ushort did = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            offset += 2;

            switch (did)
            {
                case 0xF40C: // Fuel rail pressure (2 bytes, 0.1 bar)
                    if (offset + 2 <= payload.Length)
                    {
                        double bar = ((payload[offset] << 8) | payload[offset + 1]) * 0.1;
                        snapshot.Sensors.Add(new FreezeFrameSensorValue
                        {
                            ParameterId = "DID_F40C",
                            Name = "Fuel Rail Pressure (Actual)",
                            Category = "Fuel",
                            RawValue = bar,
                            ScaledValue = Math.Round(bar, 1),
                            FormattedValue = $"{bar:F1}",
                            Unit = "bar"
                        });
                        offset += 2;
                    }
                    break;

                case 0xF40E: // Engine RPM (2 bytes, 1/4 RPM)
                    if (offset + 2 <= payload.Length)
                    {
                        double rpm = ((payload[offset] << 8) | payload[offset + 1]) * 0.25;
                        snapshot.Sensors.Add(new FreezeFrameSensorValue
                        {
                            ParameterId = "DID_F40E",
                            Name = "Engine Speed (RPM)",
                            Category = "Engine",
                            RawValue = rpm,
                            ScaledValue = Math.Round(rpm),
                            FormattedValue = $"{Math.Round(rpm)}",
                            Unit = "RPM"
                        });
                        offset += 2;
                    }
                    break;

                default:
                    // Generic DID consumer (2 bytes)
                    if (offset + 2 <= payload.Length)
                    {
                        double val = (payload[offset] << 8) | payload[offset + 1];
                        snapshot.Sensors.Add(new FreezeFrameSensorValue
                        {
                            ParameterId = $"DID_{did:X4}",
                            Name = $"UDS Data Identifier 0x{did:X4}",
                            Category = "Diagnostics",
                            RawValue = val,
                            ScaledValue = val,
                            FormattedValue = $"{val}",
                            Unit = ""
                        });
                        offset += 2;
                    }
                    break;
            }
        }

        StoreSnapshot(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Decodes OBD-II Mode 02 Freeze Frame response:
    /// Format: 0x42 [PID] [RecordNum] [DataBytes...]
    /// </summary>
    public DiagnosticFreezeFrame DecodeObdMode02(byte[] payload, string sourceEcu = "ECM")
    {
        if (payload == null || payload.Length < 3 || payload[0] != 0x42)
        {
            throw new ArgumentException("Invalid OBD-II Mode 02 response payload", nameof(payload));
        }

        byte pid = payload[1];
        byte recNum = payload[2];
        byte[] data = payload.Length > 3 ? payload[3..] : Array.Empty<byte>();

        var snapshot = _snapshots.Values.FirstOrDefault(s => s.RecordNumber == recNum && s.SourceEcu == sourceEcu)
                       ?? new DiagnosticFreezeFrame
                       {
                           RecordNumber = recNum,
                           SourceEcu = sourceEcu,
                           Protocol = "OBD-II Mode 02",
                           TimestampUtc = DateTime.UtcNow,
                           FaultTriggerReason = "Mode 02 Emission Freeze Frame"
                       };

        switch (pid)
        {
            case 0x02: // Freeze DTC
                if (data.Length >= 2)
                {
                    snapshot.DtcCode = DtcDecoder.Decode3ByteDtc(data[0], data[1], 0x00);
                }
                break;

            case 0x0C: // Engine RPM
                if (data.Length >= 2)
                {
                    double rpm = ((data[0] << 8) | data[1]) / 4.0;
                    var existingRpm = snapshot.Sensors.FirstOrDefault(s => s.ParameterId == "PID_0C");
                    if (existingRpm != null)
                    {
                        existingRpm.RawValue = rpm;
                        existingRpm.ScaledValue = Math.Round(rpm);
                        existingRpm.FormattedValue = $"{Math.Round(rpm)}";
                    }
                    else
                    {
                        snapshot.Sensors.Add(new FreezeFrameSensorValue
                        {
                            ParameterId = "PID_0C",
                            Name = "Engine Speed (RPM)",
                            Category = "Engine",
                            RawValue = rpm,
                            ScaledValue = Math.Round(rpm),
                            FormattedValue = $"{Math.Round(rpm)}",
                            Unit = "RPM"
                        });
                    }
                }
                break;

            case 0x0D: // Vehicle Speed
                if (data.Length >= 1)
                {
                    double kmh = data[0];
                    snapshot.Sensors.Add(new FreezeFrameSensorValue
                    {
                        ParameterId = "PID_0D",
                        Name = "Vehicle Speed",
                        Category = "Vehicle",
                        RawValue = kmh,
                        ScaledValue = kmh,
                        FormattedValue = $"{kmh}",
                        Unit = "km/h"
                    });
                }
                break;

            case 0x04: // Calculated Engine Load
                if (data.Length >= 1)
                {
                    double load = data[0] * 100.0 / 255.0;
                    snapshot.Sensors.Add(new FreezeFrameSensorValue
                    {
                        ParameterId = "PID_04",
                        Name = "Calculated Engine Load",
                        Category = "Engine",
                        RawValue = load,
                        ScaledValue = Math.Round(load, 1),
                        FormattedValue = $"{load:F1}",
                        Unit = "%"
                    });
                }
                break;
        }

        StoreSnapshot(snapshot);
        return snapshot;
    }

    public DiagnosticFreezeFrame GenerateSimulatedFreezeFrame(string dtcCode, string sourceEcu = "ECM")
    {
        var snapshot = new DiagnosticFreezeFrame
        {
            DtcCode = dtcCode,
            RecordNumber = 0x00,
            TimestampUtc = DateTime.UtcNow.AddMinutes(-new Random().Next(2, 45)),
            SourceEcu = sourceEcu,
            Protocol = "UDS 0x19 04 / OBD-II Mode 02",
            StatusByte = 0x2F, // Confirmed & Failed this cycle
            FaultTriggerReason = $"Fault {dtcCode} Confirmed during Operation Cycle"
        };

        // Context-dependent snapshot values based on the specific fault code
        double rpm = dtcCode switch
        {
            "P0300" => 3450.0,
            "P0171" => 2200.0,
            "P0087" => 4800.0,
            "P0299" => 3100.0,
            "P0420" => 1950.0,
            _ => 2500.0
        };

        double speedKmh = dtcCode switch
        {
            "P0300" => 78.0,
            "P0171" => 54.0,
            "P0087" => 112.0,
            "P0299" => 84.0,
            "P0420" => 65.0,
            _ => 60.0
        };

        double loadPct = dtcCode switch
        {
            "P0300" => 68.5,
            "P0171" => 28.0,
            "P0087" => 89.2,
            "P0299" => 82.0,
            "P0420" => 35.0,
            _ => 50.0
        };

        double coolantTemp = 91.5;
        double stft = dtcCode == "P0171" ? 24.2 : 1.5;
        double ltft = dtcCode == "P0171" ? 18.8 : 2.1;
        double fuelRailPressure = dtcCode == "P0087" ? 28.5 : 145.0; // P0087 low pressure fault: 28.5 bar vs 150 bar target!
        double mapKpa = dtcCode == "P0299" ? 125.0 : 195.0; // P0299 underboost: 1.25 bar vs 1.95 bar target!
        double batteryVoltage = 14.1;

        snapshot.Sensors.AddRange(new[]
        {
            new FreezeFrameSensorValue { ParameterId = "PID_0C", Name = "Engine Speed (RPM)", Category = "Engine", RawValue = rpm, ScaledValue = rpm, FormattedValue = $"{Math.Round(rpm)}", Unit = "RPM" },
            new FreezeFrameSensorValue { ParameterId = "PID_0D", Name = "Vehicle Speed", Category = "Vehicle", RawValue = speedKmh, ScaledValue = speedKmh, FormattedValue = $"{speedKmh}", Unit = "km/h" },
            new FreezeFrameSensorValue { ParameterId = "PID_04", Name = "Calculated Engine Load", Category = "Engine", RawValue = loadPct, ScaledValue = loadPct, FormattedValue = $"{loadPct:F1}", Unit = "%" },
            new FreezeFrameSensorValue { ParameterId = "PID_05", Name = "Engine Coolant Temp", Category = "Thermals", RawValue = coolantTemp, ScaledValue = coolantTemp, FormattedValue = $"{coolantTemp:F1}", Unit = "°C" },
            new FreezeFrameSensorValue { ParameterId = "PID_06", Name = "Short Term Fuel Trim Bank 1", Category = "Fuel", RawValue = stft, ScaledValue = stft, FormattedValue = $"{stft:+0.0;-0.0;0.0}", Unit = "%" },
            new FreezeFrameSensorValue { ParameterId = "PID_07", Name = "Long Term Fuel Trim Bank 1", Category = "Fuel", RawValue = ltft, ScaledValue = ltft, FormattedValue = $"{ltft:+0.0;-0.0;0.0}", Unit = "%" },
            new FreezeFrameSensorValue { ParameterId = "PID_0B", Name = "Intake Manifold Pressure (MAP)", Category = "Airflow", RawValue = mapKpa, ScaledValue = mapKpa, FormattedValue = $"{mapKpa:F0}", Unit = "kPa" },
            new FreezeFrameSensorValue { ParameterId = "PID_23", Name = "Fuel Rail Pressure (High)", Category = "Fuel", RawValue = fuelRailPressure, ScaledValue = fuelRailPressure, FormattedValue = $"{fuelRailPressure:F1}", Unit = "bar" },
            new FreezeFrameSensorValue { ParameterId = "PID_42", Name = "Control Module Battery Voltage", Category = "Electrical", RawValue = batteryVoltage, ScaledValue = batteryVoltage, FormattedValue = $"{batteryVoltage:F2}", Unit = "V" }
        });

        return snapshot;
    }
}
