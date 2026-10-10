using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Dtc;

public static class DtcDecoder
{
    private static readonly Dictionary<string, string> StandardDtcDescriptions = new()
    {
        { "P0100", "Mass or Volume Air Flow Circuit Malfunction" },
        { "P0115", "Engine Coolant Temperature Circuit Malfunction" },
        { "P0120", "Throttle/Pedal Position Sensor Circuit A" },
        { "P0300", "Random/Multiple Cylinder Misfire Detected" },
        { "P0301", "Cylinder 1 Misfire Detected" },
        { "P0420", "Catalyst System Efficiency Below Threshold" },
        { "P0500", "Vehicle Speed Sensor Malfunction" },
        { "P0606", "ECM/PCM Processor Fault" },
        { "C0035", "Left Front Wheel Speed Circuit Malfunction" },
        { "C0040", "Right Front Wheel Speed Circuit Malfunction" },
        { "C0045", "Left Rear Wheel Speed Circuit Malfunction" },
        { "C0050", "Right Rear Wheel Speed Circuit Malfunction" },
        { "B1000", "Electronic Control Unit Performance" },
        { "B1325", "Device Power 1 Circuit Voltage Low" },
        { "U0100", "Lost Communication With ECM/PCM" },
        { "U0121", "Lost Communication With Anti-Lock Brake System (ABS)" },
        { "U0140", "Lost Communication With Body Control Module (BCM)" },

        // Maruti Suzuki Swift Diesel (Magneti Marelli MJD) Reference Scanner DTCs
        { "P0235", "BOOST PRESSURE SENSOR CRT (Boost Pressure Sensor Circuit Malfunction)" },
        { "P0180", "FUEL TEMP SEN CRT (Fuel Temperature Sensor A Circuit Malfunction)" },
        { "P0030", "H02S Heater Control Circuit (Bank 1 Sensor 1)" },
        { "P0110", "IAT SENSOR CIRCUIT (Intake Air Temperature Sensor Circuit)" },
        { "P0380", "GLOW PLUG CIRCUIT (Glow Plug / Heater Circuit A)" },
        { "P0571", "BRAKE LIGHT SWITCH CIRCUIT (Brake Switch Circuit Malfunction)" },
        { "P1120", "ACCEL PEDAL POS SEN 1 CRT (Accelerator Pedal Position Sensor 1 Circuit)" },
        { "P1481", "RAD FAN OUTPUT 1 CRT MALF (Radiator Fan Relay 1 Control Circuit)" },
        { "P1190", "FUEL PRESS REGLTR FLOW (Fuel Pressure Regulator Flow Malfunction)" },
        { "P1530", "A/C COMPRESSOR RELAY CIRCUIT (Air Conditioning Compressor Relay Circuit)" },
        { "P0190", "FUEL RAIL PRESS SENS CRT (Fuel Rail Pressure Sensor Circuit)" },

        // Mahindra Thar / Scorpio (Bosch EDC16C39 CRDe) OEM DTCs
        { "P0223", "APP SENSOR 2 CIRCUIT HIGH (Throttle/Pedal Position Sensor B Circuit High Input)" },
        { "P0183", "FUEL TEMP SENSOR CIRCUIT HIGH (Fuel Temperature Sensor A Circuit High / Open)" },
        { "P0113", "IAT SENSOR CIRCUIT HIGH (Intake Air Temperature Sensor 1 Circuit High / Open)" },
        { "P1532", "A/C CLUTCH CONTROL / EVAP SENSOR CRT (A/C Compressor Control Relay Circuit Malfunction)" },
        { "P0102", "MAF SENSOR CIRCUIT LOW (Mass Air Flow Sensor A Circuit Low Input / Open)" },
        { "P1832", "4WD TRANSFER CASE SHIFT SYSTEM (4WD Transfer Case Shift Motor / Neutral Switch Malfunction)" },
        { "P1792", "BAROMETRIC SENSOR / TCU CAN TIMEOUT (Atmospheric Pressure Sensor Circuit Plausibility)" },
        { "P0341", "CAMSHAFT SENSOR CIRCUIT RANGE/PERF (Camshaft Position Sensor A Circuit Signal Synchronization)" }
    };

    public static List<DiagnosticTroubleCode> ParseUds0x19Response(byte[] payload, string ecuName = "ECU", uint canId = 0x7E8)
    {
        var list = new List<DiagnosticTroubleCode>();
        // UDS 0x19 0x02 Response: 0x59 0x02 <DTCStatusAvailabilityMask> [DTC_MSB DTC_MID DTC_LSB StatusByte]...
        if (payload == null || payload.Length < 3 || payload[0] != 0x59)
        {
            return list;
        }

        byte subfunction = payload[1];
        byte statusAvailabilityMask = payload[2];

        int offset = 3;
        while (offset + 3 < payload.Length)
        {
            byte dtcHigh = payload[offset];
            byte dtcMid = payload[offset + 1];
            byte dtcLow = payload[offset + 2];
            byte status = payload[offset + 3];
            offset += 4;

            if (dtcHigh == 0 && dtcMid == 0 && dtcLow == 0) continue;

            string dtcCode = Decode3ByteDtc(dtcHigh, dtcMid, dtcLow);
            StandardDtcDescriptions.TryGetValue(dtcCode, out var desc);

            uint rawDtc = (uint)((dtcHigh << 16) | (dtcMid << 8) | dtcLow);
            uint reqId = canId >= 0x7E8 && canId <= 0x7EF ? canId - 8 : (canId > 8 ? canId - 8 : 0x7E0);

            list.Add(new DiagnosticTroubleCode
            {
                Code = dtcCode,
                RawDtcNumber = rawDtc,
                StatusByte = status,
                Description = desc ?? "Diagnostic Trouble Code",
                EcuName = ecuName,
                CanId = canId,
                RequestCanId = reqId,
                EcuAddress = $"0x{canId:X3}",
                Protocol = "UDS",
                Service = 0x19,
                Subfunction = subfunction,
                RawDtcBytesHex = $"{dtcHigh:X2} {dtcMid:X2} {dtcLow:X2}",
                RawCompleteResponseHex = BitConverter.ToString(payload).Replace("-", " "),
                SourceDatabase = "ISO 14229 / SAE J2012 OEM Matrix",
                Confidence = (status & 0x08) != 0 ? "CONFIRMED" : ((status & 0x04) != 0 ? "PENDING" : "ACTIVE"),
                TimestampUtc = DateTime.UtcNow
            });
        }

        return list;
    }

    public static string Decode3ByteDtc(byte highByte, byte midByte, byte lowByte)
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
        int d3 = (midByte >> 4) & 0x0F;
        int d4 = midByte & 0x0F;
        // Low byte is fault type / symptom byte (e.g. 0x00, 0x12, 0x2A)
        return $"{prefix}{d1:X1}{d2:X1}{d3:X1}{d4:X1}";
    }

    public static string GetDescription(string dtcCode)
    {
        StandardDtcDescriptions.TryGetValue(dtcCode, out var desc);
        return desc ?? "Diagnostic Trouble Code";
    }

    public static List<DiagnosticTroubleCode> ParseKwp0x58or0x53Response(byte[] payload, string ecuName = "ECU", uint canId = 0x7E8)
    {
        var list = new List<DiagnosticTroubleCode>();
        if (payload == null || payload.Length < 2)
        {
            return list;
        }

        byte sid = payload[0];
        if (sid != 0x58 && sid != 0x53)
        {
            return list;
        }

        byte reportedCount = payload[1];
        if (reportedCount == 0 && payload.Length <= 2)
        {
            return list;
        }

        int remainingBytes = payload.Length - 2;
        // In KWP2000, each DTC is either 3 bytes (High, Low, Status) or 2 bytes (High, Low)
        int recordSize = 3;
        if (reportedCount > 0 && remainingBytes == reportedCount * 3)
        {
            recordSize = 3;
        }
        else if (reportedCount > 0 && remainingBytes == reportedCount * 2)
        {
            recordSize = 2;
        }
        else if (remainingBytes % 3 == 0 && remainingBytes / 3 == reportedCount)
        {
            recordSize = 3;
        }
        else if (remainingBytes % 2 == 0 && remainingBytes / 2 == reportedCount)
        {
            recordSize = 2;
        }
        else if (remainingBytes % 3 == 0)
        {
            recordSize = 3;
        }
        else if (remainingBytes % 2 == 0)
        {
            recordSize = 2;
        }

        int offset = 2;
        int maxCodes = reportedCount > 0 ? reportedCount : 255;
        while (offset + 1 < payload.Length && list.Count < maxCodes)
        {
            byte dtcHigh = payload[offset];
            byte dtcLow = payload[offset + 1];
            byte status = (recordSize >= 3 && offset + 2 < payload.Length) ? payload[offset + 2] : (byte)0x08; // Confirmed
            offset += recordSize;

            if (dtcHigh == 0 && dtcLow == 0) continue;

            string dtcCode = Decode2ByteDtc(dtcHigh, dtcLow);
            StandardDtcDescriptions.TryGetValue(dtcCode, out var desc);

            uint rawDtc = (uint)((dtcHigh << 8) | dtcLow);
            uint reqId = canId >= 0x7E8 && canId <= 0x7EF ? canId - 8 : (canId > 8 ? canId - 8 : 0x7E0);

            list.Add(new DiagnosticTroubleCode
            {
                Code = dtcCode,
                RawDtcNumber = rawDtc,
                StatusByte = status,
                Description = desc ?? GetDescription(dtcCode),
                EcuName = ecuName,
                CanId = canId,
                RequestCanId = reqId,
                EcuAddress = $"0x{canId:X3}",
                Protocol = "KWP2000",
                Service = (byte)(sid - 0x40),
                Subfunction = null,
                RawDtcBytesHex = $"{dtcHigh:X2} {dtcLow:X2}",
                RawCompleteResponseHex = BitConverter.ToString(payload).Replace("-", " "),
                SourceDatabase = "ISO 14230 / KWP2000 Bosch EDC16 Matrix",
                Confidence = "CONFIRMED",
                TimestampUtc = DateTime.UtcNow
            });
        }

        return list;
    }

    public static List<DiagnosticTroubleCode> ParseJ1939DmResponse(byte[] payload, string ecuName = "J1939_ECU", uint canId = 0x18FECA00, bool isDm1 = true)
    {
        var list = new List<DiagnosticTroubleCode>();
        if (payload == null || payload.Length < 6) return list;

        // Bytes 0-1: Lamp status
        int offset = 2;
        while (offset + 3 < payload.Length)
        {
            byte b0 = payload[offset];
            byte b1 = payload[offset + 1];
            byte b2 = payload[offset + 2];
            byte b3 = payload[offset + 3];
            offset += 4;

            // SPN: bits 0-18 (b0 is bits 0-7, b1 is bits 8-15, b2 bits 5-7 is SPN bits 16-18)
            uint spn = (uint)(b0 | (b1 << 8) | ((b2 >> 5) << 16));
            byte fmi = (byte)(b2 & 0x1F);
            byte occ = (byte)(b3 & 0x7F);

            if (spn == 0 || spn == 0x7FFFF) continue;

            string code = $"SPN{spn}_FMI{fmi}";
            list.Add(new DiagnosticTroubleCode
            {
                Code = code,
                RawDtcNumber = (spn << 8) | fmi,
                StatusByte = isDm1 ? (byte)0x09 : (byte)0x08, // Active / Previously Active
                Description = $"J1939 SPN {spn} (FMI {fmi}): {GetJ1939FmiDescription(fmi)}",
                EcuName = ecuName,
                CanId = canId,
                RequestCanId = 0x18EAFF00,
                EcuAddress = $"0x{(canId & 0xFF):X2}",
                Protocol = "J1939",
                Service = isDm1 ? (byte)0x01 : (byte)0x02,
                Subfunction = null,
                RawDtcBytesHex = $"{b0:X2} {b1:X2} {b2:X2} {b3:X2}",
                RawCompleteResponseHex = BitConverter.ToString(payload).Replace("-", " "),
                SourceDatabase = "SAE J1939 Commercial Vehicle Specification",
                Confidence = "CONFIRMED",
                OccurrenceCount = occ,
                TimestampUtc = DateTime.UtcNow
            });
        }
        return list;
    }

    private static string GetJ1939FmiDescription(byte fmi) => fmi switch
    {
        0 => "Data Valid But Above Normal Operational Range (Most Severe)",
        1 => "Data Valid But Below Normal Operational Range (Most Severe)",
        2 => "Data Erratic, Intermittent, Or Incorrect",
        3 => "Voltage Above Normal, Or Shorted To High Source",
        4 => "Voltage Below Normal, Or Shorted To Low Source",
        5 => "Current Below Normal Or Open Circuit",
        6 => "Current Above Normal Or Grounded Circuit",
        7 => "Mechanical System Not Responding Or Out Of Adjustment",
        8 => "Abnormal Frequency Or Pulse Width Or Period",
        9 => "Abnormal Update Rate",
        10 => "Abnormal Rate Of Change",
        11 => "Root Cause Not Known",
        12 => "Bad Intelligent Device Or Component",
        13 => "Out Of Calibration",
        14 => "Special Instructions",
        15 => "Data Valid But Above Normal Range (Least Severe)",
        16 => "Data Valid But Above Normal Range (Moderately Severe)",
        17 => "Data Valid But Below Normal Range (Least Severe)",
        18 => "Data Valid But Below Normal Range (Moderately Severe)",
        31 => "Condition Exists",
        _ => $"Failure Mode Identifier {fmi}"
    };

    public static string Decode2ByteDtc(byte highByte, byte lowByte)
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
}
