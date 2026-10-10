using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Analysis.OperatingEnvelope;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Signals;

namespace CanEngineeringPlatform.Tests;

public class UniversalDiagnosticAndTuningTests
{
    // =========================================================================
    // 1. Unified Canonical Signal Registry Tests
    // =========================================================================

    [Fact]
    public void UnifiedSignalRegistry_ResolvesOemAliasesToCanonicalIds()
    {
        var registry = new UnifiedSignalRegistry();

        Assert.True(registry.TryResolveCanonicalName("EngineRPM", out var id1));
        Assert.Equal("EngineRPM", id1);

        Assert.True(registry.TryResolveCanonicalName("MO_Drehzahl", out var id2));
        Assert.Equal("EngineRPM", id2);

        Assert.True(registry.TryResolveCanonicalName("CR_Ems_EngSpd", out var id3));
        Assert.Equal("EngineRPM", id3);

        Assert.True(registry.TryResolveCanonicalName("EngSpd", out var id4));
        Assert.Equal("EngineRPM", id4);

        Assert.True(registry.TryResolveCanonicalName("Fahrgeschwindigkeit", out var id5));
        Assert.Equal("VehicleSpeed", id5);

        Assert.True(registry.TryResolveCanonicalName("DK_Winkel", out var id6));
        Assert.Equal("ThrottlePosition", id6);

        Assert.True(registry.TryResolveCanonicalName("Kraftstoffdruck", out var id7));
        Assert.Equal("FuelRailPressure", id7);
    }

    [Fact]
    public void UnifiedSignalRegistry_IngestSignal_UpdatesValueAndMinMax()
    {
        var registry = new UnifiedSignalRegistry();

        registry.IngestSignal("MO_Drehzahl", 2400.0, "rpm");
        var sig = registry.GetSignal("EngineRPM");

        Assert.NotNull(sig);
        Assert.Equal(2400.0, sig.Value);
        Assert.Equal("MO_Drehzahl", sig.RawSourceName);
        Assert.Equal(2400.0, sig.MinObserved);
        Assert.Equal(2400.0, sig.MaxObserved);
        Assert.True(sig.IsActive);
        Assert.Equal(SignalQuality.Valid, sig.Quality);

        registry.IngestSignal("EngineSpeed", 3100.0, "rpm");
        Assert.Equal(3100.0, sig.Value);
        Assert.Equal(2400.0, sig.MinObserved);
        Assert.Equal(3100.0, sig.MaxObserved);
        Assert.Equal(2, sig.UpdateCount);
    }

    [Fact]
    public void UnifiedSignalRegistry_IngestDecodedSignals_BatchUpdates()
    {
        var registry = new UnifiedSignalRegistry();

        var signals = new List<DecodedSignal>
        {
            new DecodedSignal { SignalName = "EngineRPM", EngineeringValue = 1850.0, Unit = "rpm" },
            new DecodedSignal { SignalName = "VehicleSpeed", EngineeringValue = 72.5, Unit = "km/h" },
            new DecodedSignal { SignalName = "EngineCoolantTemp", EngineeringValue = 89.0, Unit = "°C" }
        };

        registry.IngestDecodedSignals(signals);

        var rpm = registry.GetSignal("EngineRPM");
        var spd = registry.GetSignal("VehicleSpeed");
        var ect = registry.GetSignal("EngineCoolantTemperature");

        Assert.NotNull(rpm);
        Assert.Equal(1850.0, rpm.Value);
        Assert.NotNull(spd);
        Assert.Equal(72.5, spd.Value);
        Assert.NotNull(ect);
        Assert.Equal(89.0, ect.Value);
    }

    // =========================================================================
    // 2. OBD Mode 01 Standardized PID Discovery & Decoding Tests
    // =========================================================================

    [Fact]
    public void ObdDecoder_SupportedPidsBitmap_DecodesExactPids()
    {
        // Example bitmap 0xBE1FA813:
        // Binary: 10111110 00011111 10101000 00010011
        // Bits set correspond to PIDs 1, 3, 4, 5, 6, 7, 12 (0x0C), 13 (0x0D), 14, 15 (0x0F), 16 (0x10), 17 (0x11), 19, 21, 28, 31, 32 (0x20)
        uint bitmap = 0xBE1FA813;
        var supported = ObdDecoder.DecodeSupportedPidsBitmap(0x00, bitmap);

        Assert.Contains((byte)0x01, supported); // Monitor status
        Assert.Contains((byte)0x04, supported); // Engine load
        Assert.Contains((byte)0x05, supported); // Coolant temp
        Assert.Contains((byte)0x0C, supported); // Engine speed
        Assert.Contains((byte)0x0D, supported); // Vehicle speed
        Assert.Contains((byte)0x0F, supported); // Intake air temp
        Assert.Contains((byte)0x10, supported); // MAF rate
        Assert.Contains((byte)0x11, supported); // Throttle
        Assert.Contains((byte)0x20, supported); // Next block support
    }

    [Fact]
    public void ObdDecoder_DecodesExtendedStandardPidsAccurately()
    {
        // PID 0x06: Short Term Fuel Trim Bank 1 (A = 128 -> 0%)
        byte[] p06 = new byte[] { 0x41, 0x06, 128 };
        var resp06 = ObdDecoder.ParseResponse(p06);
        Assert.True(resp06.IsPositive);
        Assert.Single(resp06.PidValues);
        Assert.Equal(0.0, resp06.PidValues[0].Value);

        // PID 0x0B: MAP (A = 101 kPa)
        byte[] p0b = new byte[] { 0x41, 0x0B, 101 };
        var resp0b = ObdDecoder.ParseResponse(p0b);
        Assert.True(resp0b.IsPositive);
        Assert.Equal(101.0, resp0b.PidValues[0].Value);

        // PID 0x5E: Fuel Rate (Raw = 200 -> 10.00 L/h)
        byte[] p5e = new byte[] { 0x41, 0x5E, 0x00, 200 };
        var resp5e = ObdDecoder.ParseResponse(p5e);
        Assert.True(resp5e.IsPositive);
        Assert.Equal(10.0, resp5e.PidValues[0].Value);
    }

    [Fact]
    public void ObdDecoder_ParsesMode0A_PermanentDtcs()
    {
        // Mode 0A positive response SID = 0x4A, P0300 (0x03 0x00)
        byte[] payload = new byte[] { 0x4A, 0x03, 0x00 };
        var resp = ObdDecoder.ParseResponse(payload);

        Assert.True(resp.IsPositive);
        Assert.Equal(ObdMode.PermanentDTCs, resp.Mode);
        Assert.Single(resp.DtcCodes);
        Assert.Equal("P0300", resp.DtcCodes[0]);
    }

    // =========================================================================
    // 3. Evidentiary DTC Forensic Tracking Tests
    // =========================================================================

    [Fact]
    public void DtcDecoder_ParseUds0x19Response_ContainsFullForensicFields()
    {
        // UDS 0x19 0x02 Response: 0x59 0x02 0xFF (P0235 = 0x02 0x35 0x00, Status = 0x2F)
        byte[] payload = new byte[] { 0x59, 0x02, 0xFF, 0x02, 0x35, 0x00, 0x2F };
        var dtcs = DtcDecoder.ParseUds0x19Response(payload, "ECM", 0x7E8);

        Assert.Single(dtcs);
        var dtc = dtcs[0];
        Assert.Equal("P0235", dtc.Code);
        Assert.Equal("UDS", dtc.Protocol);
        Assert.Equal((byte)0x19, dtc.Service);
        Assert.Equal((byte)0x02, dtc.Subfunction);
        Assert.Equal((uint)0x7E0, dtc.RequestCanId);
        Assert.Equal((uint)0x7E8, dtc.ResponseCanId);
        Assert.Equal("0x7E8", dtc.EcuAddress);
        Assert.Equal("02 35 00", dtc.RawDtcBytesHex);
        Assert.Contains("59 02 FF", dtc.RawCompleteResponseHex);
        Assert.Equal("CONFIRMED", dtc.Confidence);
    }

    [Fact]
    public void DtcDecoder_ParseJ1939DmResponse_DecodesSpnAndFmiForensics()
    {
        // J1939 DM1 packet:
        // Lamps: 0x00 0x00
        // DTC: SPN 190 (Engine Speed = 0xBE 0x00 0x00), FMI 2 (Erratic), Occurrence 3
        // Byte 0: 0xBE (SPN 0-7)
        // Byte 1: 0x00 (SPN 8-15)
        // Byte 2: (0 << 5) | (2 & 0x1F) = 0x02 (SPN 16-18 + FMI)
        // Byte 3: 0x03 (Occurrence Count)
        byte[] payload = new byte[] { 0x00, 0x00, 0xBE, 0x00, 0x02, 0x03 };
        var dtcs = DtcDecoder.ParseJ1939DmResponse(payload, "J1939_ENGINE", 0x18FECA00, isDm1: true);

        Assert.Single(dtcs);
        var dtc = dtcs[0];
        Assert.Equal("SPN190_FMI2", dtc.Code);
        Assert.Equal("J1939", dtc.Protocol);
        Assert.Equal(3, dtc.OccurrenceCount);
        Assert.Equal("CONFIRMED", dtc.Confidence);
        Assert.Contains("Erratic", dtc.Description);
    }

    // =========================================================================
    // 4. Live CAN Tuning Analysis Table (Operating Envelope) Tests
    // =========================================================================

    [Fact]
    public void LiveOperatingGridService_AccumulatesStatisticalCellBuckets()
    {
        var gridService = new LiveOperatingGridService();
        var snapInitial = gridService.GetSnapshot("rpm_vs_load_lambda");

        Assert.NotNull(snapInitial);
        Assert.Contains("LIVE TELEMETRY OPERATING ENVELOPE ONLY", snapInitial.Disclaimer);
        Assert.Equal(0, snapInitial.TotalSamples);

        // Feed telemetry points:
        // RPM = 2000 (X index ~ 3), Load = 50% (Y index ~ 4), Lambda = 0.98, 1.00, 1.02
        gridService.IngestPoint("rpm_vs_load_lambda", 2000, 50, 0.98);
        gridService.IngestPoint("rpm_vs_load_lambda", 2000, 50, 1.00);
        gridService.IngestPoint("rpm_vs_load_lambda", 2000, 50, 1.02);

        var snap = gridService.GetSnapshot("rpm_vs_load_lambda");
        Assert.Equal(3, snap.TotalSamples);
        Assert.Equal(1, snap.ActiveCellCount);

        // Verify cell statistics
        var cell = snap.Cells.SelectMany(row => row).First(c => c.HasData);
        Assert.Equal(3, cell.SampleCount);
        Assert.Equal(1.0, cell.Mean!.Value);
        Assert.Equal(0.98, cell.Min!.Value);
        Assert.Equal(1.02, cell.Max!.Value);
        Assert.Equal(1.02, cell.Latest!.Value);
        Assert.True(cell.StdDev!.Value > 0.0);
    }

    [Fact]
    public void LiveOperatingGridService_UnsampledCells_RenderAsNoDataInCsv()
    {
        var gridService = new LiveOperatingGridService();
        gridService.IngestPoint("rpm_vs_load_lambda", 1200, 20, 1.01);

        string csv = gridService.ExportToCsv("rpm_vs_load_lambda");
        Assert.Contains("LIVE TELEMETRY OPERATING ENVELOPE ONLY", csv);
        Assert.Contains("NO DATA", csv);
        Assert.Contains("1.010", csv);
    }
}
