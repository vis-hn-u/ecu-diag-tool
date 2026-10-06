using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Calibration;
using CanEngineeringPlatform.Core.Dashboard;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;
using CanEngineeringPlatform.Core.Diagnostics.Manufacturer;
using CanEngineeringPlatform.Core.Diagnostics.Transport;
using CanEngineeringPlatform.Core.Diagnostics.Vag;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Tests;

public class DiagnosticsExtendedTests
{
    [Fact]
    public async Task VagTp20_ChannelSetupAndSegmentation_ExchangesPayloads()
    {
        var simTransport = new SimulationTransport();
        await simTransport.ConnectAsync();

        var tp20 = new VagTp20DiagnosticTransport(simTransport);

        // Verify channel setup
        bool setupOk = await tp20.SetupChannelAsync(moduleAddress: 0x01, protocol: 0x01);
        Assert.True(setupOk);
        Assert.True(tp20.IsConnected);

        byte[]? receivedDiagnosticPayload = null;
        tp20.OnPayloadReceived += args =>
        {
            receivedDiagnosticPayload = args.Payload;
        };

        // Transmit diagnostic request: 0x22 0xF1 0x90 (Read VIN)
        byte[] udsRequest = new byte[] { 0x22, 0xF1, 0x90 };
        bool sent = await tp20.SendDiagnosticPayloadAsync(0x01, udsRequest);
        Assert.True(sent);

        // Simulate incoming TP2.0 multi-packet response from ECU on 0x300
        // Total payload: 62 F1 90 57 56 57 (Positive response VIN snippet, 6 bytes)
        // Packet 1: [Header: 0x00] [LenHi: 0x00] [LenLo: 0x06] [Data: 62 F1 90 57 56]
        // Packet 2: [Header: 0x11] [Data: 57]
        byte[] p1 = new byte[] { 0x00, 0x00, 0x06, 0x62, 0xF1, 0x90, 0x57, 0x56 };
        byte[] p2 = new byte[] { 0x11, 0x57 };

        tp20.HandleCanFrame(CanFrame.CreateClassic(0x300, p1, channel: 1));
        tp20.HandleCanFrame(CanFrame.CreateClassic(0x300, p2, channel: 1));

        Assert.NotNull(receivedDiagnosticPayload);
        Assert.Equal(6, receivedDiagnosticPayload.Length);
        Assert.Equal(0x62, receivedDiagnosticPayload[0]);
        Assert.Equal(0xF1, receivedDiagnosticPayload[1]);
        Assert.Equal(0x90, receivedDiagnosticPayload[2]);
        Assert.Equal((byte)'W', receivedDiagnosticPayload[3]);
        Assert.Equal((byte)'V', receivedDiagnosticPayload[4]);
        Assert.Equal((byte)'W', receivedDiagnosticPayload[5]);

        tp20.Dispose();
        await simTransport.DisconnectAsync();
    }

    [Fact]
    public async Task Kwp2000_EncapsulationAndServiceDispatch_Succeeds()
    {
        var simTransport = new SimulationTransport();
        await simTransport.ConnectAsync();

        var kwp = new Kwp2000DiagnosticTransport(simTransport);

        byte[]? receivedPayload = null;
        kwp.OnPayloadReceived += args =>
        {
            receivedPayload = args.Payload;
        };

        // Transmit KWP2000 service 0x21 0x01 (ReadDataByLocalIdentifier Group 1)
        byte[] kwpReq = new byte[] { 0x21, 0x01 };
        bool sent = await kwp.SendDiagnosticPayloadAsync(0x7E0, kwpReq);
        Assert.True(sent);

        // Simulate incoming KWP2000 response: 0x61 0x01 0x03 0xE8 (Positive response, 4 bytes)
        // ISO 14230-4 CAN format: Byte 0 = length (4), Bytes 1..4 = data
        byte[] kwpRespCan = new byte[] { 0x04, 0x61, 0x01, 0x03, 0xE8, 0x00, 0x00, 0x00 };
        kwp.HandleCanFrame(CanFrame.CreateClassic(0x7E8, kwpRespCan, channel: 1));

        Assert.NotNull(receivedPayload);
        Assert.Equal(4, receivedPayload.Length);
        Assert.Equal(0x61, receivedPayload[0]);
        Assert.Equal(0x01, receivedPayload[1]);
        Assert.Equal(0x03, receivedPayload[2]);
        Assert.Equal(0xE8, receivedPayload[3]);

        kwp.Dispose();
        await simTransport.DisconnectAsync();
    }

    [Fact]
    public void VagMeasuringBlocks_DecodesStandardFormulasAccurately()
    {
        var service = new VagMeasuringBlockService();

        // 1. Formula 0x01: RPM = 0.2 * A * B -> A=25, B=160 -> 0.2 * 25 * 160 = 800 RPM
        var (rpmVal, rpmFmt, rpmUnit) = service.DecodeFormula(VagFormulaType.Rpm, 25, 160);
        Assert.Equal(800.0, rpmVal);
        Assert.Equal("800", rpmFmt);
        Assert.Equal("RPM", rpmUnit);

        // 2. Formula 0x05: Temp = 0.75 * A - 48 -> A=184 -> 0.75 * 184 - 48 = 90.0 °C
        var (tempVal, tempFmt, tempUnit) = service.DecodeFormula(VagFormulaType.Temperature, 184, 0);
        Assert.Equal(90.0, tempVal);
        Assert.Equal("90.0", tempFmt);
        Assert.Equal("°C", tempUnit);

        // 3. Formula 0x24: Pressure = 0.1 * A * B -> A=100, B=215 -> 0.1 * 100 * 215 = 2150 mbar
        var (mbarVal, mbarFmt, mbarUnit) = service.DecodeFormula(VagFormulaType.PressureMbar, 100, 215);
        Assert.Equal(2150.0, mbarVal);
        Assert.Equal("2150", mbarFmt);
        Assert.Equal("mbar", mbarUnit);

        // 4. Formula 0x25: Duty Cycle = A * 100 / 255 -> A=128 -> 50.2%
        var (dutyVal, dutyFmt, dutyUnit) = service.DecodeFormula(VagFormulaType.DutyCycle, 128, 0);
        Assert.True(Math.Abs(dutyVal - 50.2) < 0.2);
        Assert.Equal("%", dutyUnit);

        // 5. Formula 0x28: Lambda = 0.001 * A * B -> A=4, B=210 -> 0.840
        var (lamVal, lamFmt, lamUnit) = service.DecodeFormula(VagFormulaType.LambdaRatio, 4, 210);
        Assert.Equal(0.840, lamVal);
        Assert.Equal("0.840", lamFmt);
        Assert.Equal("λ", lamUnit);

        // 6. Test Group 115 simulated
        var g115 = service.GetSimulatedGroup(115, rpm: 3500, boostBar: 1.2, loadPct: 85.0);
        Assert.Equal(115, g115.GroupNumber);
        Assert.Equal(4, g115.Zones.Count);
        Assert.Equal(3500, g115.Zones[0].Value);
        Assert.Equal(85.0, g115.Zones[1].Value);
        Assert.Equal(2213.0, g115.Zones[2].Value); // 1013 + 1200 = 2213 mbar
    }

    [Fact]
    public void ManufacturerLiveData_DecodesOemSpecificDids()
    {
        var service = new ManufacturerLiveDataService();
        var vagDefs = service.GetDefinitionsByOem(ManufacturerOem.VAG);
        Assert.NotEmpty(vagDefs);

        // Test VAG Fuel Rail Pressure (DID 0xF40C)
        // Factor = 0.1, Offset = 0, BigEndian 2 bytes
        // Raw = 0x05DC (1500) -> 1500 * 0.1 = 150.0 bar
        var railDef = service.GetDefinitionById("vag_fuel_rail_act");
        Assert.NotNull(railDef);

        byte[] payload = new byte[] { 0x05, 0xDC };
        var decoded = service.DecodeValue(railDef, payload);
        Assert.Equal(150.0, decoded.ScaledValue);
        Assert.Equal("bar", decoded.Unit);
        Assert.False(decoded.IsWarning);

        // Test VAG Wastegate N75 Duty (1 byte, factor 100/255)
        var n75Def = service.GetDefinitionById("vag_wastegate_n75");
        Assert.NotNull(n75Def);
        byte[] n75Payload = new byte[] { 191 }; // ~75%
        var n75Decoded = service.DecodeValue(n75Def, n75Payload);
        Assert.True(n75Decoded.ScaledValue >= 74.0 && n75Decoded.ScaledValue <= 76.0);
        Assert.Equal("%", n75Decoded.Unit);
    }

    [Fact]
    public void FreezeFrame_DecodesObdAndUdsSnapshots()
    {
        var service = new FreezeFrameSnapshotService();

        // 1. Test UDS 0x19 0x04 decoding
        // Response: 0x59 0x04 [DTC 0x03 0x00 0x00 -> P0300] [Status: 0x28] [RecordNum: 0x01] [NumIds: 0x01]
        // DID 0xF40E (Engine RPM, factor 0.25): 0x2A 0x30 (10800 * 0.25 = 2700 RPM)
        byte[] udsPayload = new byte[]
        {
            0x59, 0x04, 0x03, 0x00, 0x00, 0x28, 0x01, 0x01,
            0xF4, 0x0E, 0x2A, 0x30
        };

        var udsSnap = service.DecodeUdsSnapshot(udsPayload, "ECM");
        Assert.NotNull(udsSnap);
        Assert.Equal("P0300", udsSnap.DtcCode);
        Assert.Equal(1, udsSnap.RecordNumber);
        var rpmSensor = udsSnap.Sensors.FirstOrDefault(s => s.ParameterId == "DID_F40E");
        Assert.NotNull(rpmSensor);
        Assert.Equal(2700.0, rpmSensor.ScaledValue);

        // 2. Test OBD Mode 02 decoding
        // Response: 0x42 [PID 0x0C Engine RPM] [Record 0x00] [Data: 0x32 0x00 -> (12800 / 4) = 3200 RPM]
        byte[] obdPayload = new byte[] { 0x42, 0x0C, 0x00, 0x32, 0x00 };
        var obdSnap = service.DecodeObdMode02(obdPayload, "ECM");
        Assert.NotNull(obdSnap);
        var obdRpm = obdSnap.Sensors.FirstOrDefault(s => s.ParameterId == "PID_0C");
        Assert.NotNull(obdRpm);
        Assert.Equal(3200.0, obdRpm.ScaledValue);
    }

    [Fact]
    public void DtcCorrelation_IdentifiesAnomalousSensorDeviations()
    {
        var service = new DtcCorrelationService();

        // 1. Test knowledge base metadata
        var p0087Meta = service.GetMetadata("P0087");
        Assert.NotNull(p0087Meta);
        Assert.Equal(DtcSeverity.Major, p0087Meta.Severity);
        Assert.Contains("High Pressure Fuel Pump", string.Join(" ", p0087Meta.LikelyCauses));

        // 2. Test contextual correlation on a dataset with an intentional pressure collapse anomaly
        var dataset = new CanonicalLogDataSet { Name = "WOT Telemetry Run" };

        var rpmChan = new TimeSeriesChannel("Engine Speed", "RPM");
        var railChan = new TimeSeriesChannel("Fuel Rail Pressure", "bar");
        var boostChan = new TimeSeriesChannel("Boost Pressure", "bar");

        // Normal baseline: 40 points
        for (int i = 0; i < 40; i++)
        {
            rpmChan.AddPoint(i * 100, 3000 + i * 50);
            railChan.AddPoint(i * 100, 145.0 + (i % 3)); // steady ~146 bar
            boostChan.AddPoint(i * 100, 1.4);
        }

        // Moment of fault: rail pressure collapses to 25 bar!
        rpmChan.AddPoint(4100, 5050);
        railChan.AddPoint(4100, 25.0); // Severe deviation!
        boostChan.AddPoint(4100, 1.4);

        dataset.Channels["Engine Speed"] = rpmChan;
        dataset.Channels["Fuel Rail Pressure"] = railChan;
        dataset.Channels["Boost Pressure"] = boostChan;

        var result = service.CorrelateDtcWithTelemetry("P0087", dataset);
        Assert.NotNull(result);
        Assert.Equal("Fuel Rail Pressure", result.TopSuspectSignal);
        Assert.True(result.Anomalies[0].IsPrimarySuspect);
        Assert.True(result.Anomalies[0].DeviationZScore > 4.0);
        Assert.True(result.Anomalies[0].DeviationPct < -70.0);
    }

    [Fact]
    public void EcuDefinitionPack_MatchesHwSwAndLoadsReadonlyMaps()
    {
        var service = new EcuDefinitionPackService();
        var packs = service.GetAllPacks();
        Assert.NotEmpty(packs);

        // 1. Verify strict read-only property
        Assert.All(packs, p => Assert.True(p.IsReadOnly));

        // 2. Match by MED17 Hardware Part Number
        var med17 = service.MatchPack("06J907309B", "0030", null);
        Assert.NotNull(med17);
        Assert.Equal("med17_ea888_gen2", med17.Id);

        // 3. Inspect KFZW ignition map
        var kfzw = service.GetMap(med17.Id, "KFZW");
        Assert.NotNull(kfzw);
        Assert.True(kfzw.IsReadOnly);
        Assert.Equal(MapCategory.Ignition, kfzw.Category);
        Assert.Equal("°BTDC", kfzw.ZUnit);
        Assert.Equal(12, kfzw.XAxis.Breakpoints.Length); // 12 RPM points
        Assert.Equal(8, kfzw.YAxis!.Breakpoints.Length);  // 8 Load points

        // 4. Test Operating Point Tracer (RPM = 3500, Load = 120%)
        var tracer = service.CalculateOperatingPoint(med17.Id, "KFZW", rpm: 3500, load: 120.0);
        Assert.NotNull(tracer);
        Assert.Equal(5, tracer.NearestXIndex); // 3500 RPM
        Assert.Equal(4, tracer.NearestYIndex); // 120% Load
        Assert.True(tracer.InterpolatedZValue > 0);
    }

    [Fact]
    public void DashboardDesigner_ManagesLayoutsAndPresets()
    {
        var service = new DashboardDesignerService();
        var layouts = service.GetAllLayouts();
        Assert.NotEmpty(layouts);

        // 1. Verify track cockpit preset
        var trackPreset = service.LoadPreset("track_cockpit");
        Assert.NotNull(trackPreset);
        Assert.Contains(trackPreset.Widgets, w => w.Type == DashboardWidgetType.RadialGauge && w.Unit == "RPM");
        Assert.Contains(trackPreset.Widgets, w => w.Type == DashboardWidgetType.RadialGauge && w.Unit == "bar");
        Assert.Contains(trackPreset.Widgets, w => w.Type == DashboardWidgetType.DigitalValue && w.Unit == "λ");
        Assert.Contains(trackPreset.Widgets, w => w.Type == DashboardWidgetType.StatusAnnunciator);

        // 2. Save custom layout
        var customLayout = new DashboardLayout
        {
            Id = "custom_tuner_1",
            Name = "Custom Drag Strip Cluster",
            Widgets = new List<DashboardWidget>
            {
                new() { Id = "w1", Title = "BOOST", Type = DashboardWidgetType.RadialGauge, Unit = "psi", MaxValue = 35.0 }
            }
        };

        service.SaveLayout(customLayout);
        var retrieved = service.GetLayoutById("custom_tuner_1");
        Assert.NotNull(retrieved);
        Assert.Equal("Custom Drag Strip Cluster", retrieved.Name);
        Assert.Single(retrieved.Widgets);

        // 3. Delete custom layout
        bool deleted = service.DeleteLayout("custom_tuner_1");
        Assert.True(deleted);
        Assert.Null(service.GetLayoutById("custom_tuner_1"));
    }
}
