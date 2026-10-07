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
        { "P0190", "FUEL RAIL PRESS SENS CRT (Fuel Rail Pressure Sensor Circuit)" }
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

            list.Add(new DiagnosticTroubleCode
            {
                Code = dtcCode,
                RawDtcNumber = rawDtc,
                StatusByte = status,
                Description = desc ?? "Diagnostic Trouble Code",
                EcuName = ecuName,
                CanId = canId,
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
}
