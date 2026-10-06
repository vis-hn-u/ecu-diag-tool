using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Ai;
using CanEngineeringPlatform.Core.Analysis.ReverseEngineering;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Hil;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Projects;
using CanEngineeringPlatform.Core.Protocols.Asam;
using CanEngineeringPlatform.Core.Research;
using CanEngineeringPlatform.Core.Sessions.Timeline;
using CanEngineeringPlatform.Core.Simulation.DigitalTwin;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Tests;

public class UltimateProductizationTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // 1. PROJECT WORKSPACE TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ProjectService_CreateAndRetrieveProject_Succeeds()
    {
        var svc = new ProjectService();
        var proj = svc.CreateProject("Test BMW S63 Project", "High performance twin-turbo bench test", "Engineer V");

        Assert.NotNull(proj);
        Assert.Equal("Test BMW S63 Project", proj.Name);
        Assert.Equal("Engineer V", proj.CreatedBy);

        var retrieved = svc.GetProject(proj.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(proj.Id, retrieved.Id);
    }

    [Fact]
    public void ProjectService_AddEvidenceAndComputeHash_MaintainsProvenance()
    {
        var svc = new ProjectService();
        var proj = svc.CreateProject("Evidence Integrity Test");

        byte[] fakeCapture = new byte[] { 0xAA, 0x55, 0x01, 0x02, 0x03, 0x04 };
        var evidence = svc.AddEvidence(proj.Id, "CAN Capture", "Dyno 3rd gear pull log", "Waveshare CAN-FD", fakeCapture);

        Assert.NotNull(evidence);
        Assert.False(string.IsNullOrEmpty(evidence.Sha256Hash));

        string projectHash = svc.ComputeProjectHash(proj.Id);
        Assert.False(string.IsNullOrEmpty(projectHash));
    }

    [Fact]
    public void ProjectService_ExportAndImportJson_PreservesCompleteStructure()
    {
        var svc = new ProjectService();
        var proj = svc.CreateProject("Export Import Verification");
        svc.AddNote(proj.Id, "Tuning Note", "Target boost set to 1.85 bar relative", "Calibration");

        string json = svc.ExportProjectJson(proj.Id);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var imported = svc.ImportProjectJson(json);
        Assert.NotNull(imported);
        Assert.Equal("Export Import Verification", imported.Name);
        Assert.Single(imported.Notes);
        Assert.Equal("Tuning Note", imported.Notes[0].Title);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. UNIFIED FORENSIC EVIDENCE TIMELINE TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UnifiedTimelineService_RecordAndFilter_ChronologicallyPreserved()
    {
        var timeline = new UnifiedTimelineService();

        timeline.Record(TimelineCategory.HardwareEvent, "VCI Connected", "USB-CAN-FD-B online", TimelineSeverity.Info, "HARDWARE");
        timeline.Record(TimelineCategory.DtcEvent, "DTC P0300 Detected", "Misfire random", TimelineSeverity.Warning, "DIAGNOSTICS");
        timeline.Record(TimelineCategory.FlashOperation, "Sector 0 Erase", "Flash sector erased", TimelineSeverity.Notice, "PROGRAMMER");

        Assert.Equal(3, timeline.GetTotalCount());

        var allEntries = timeline.GetEntries(10);
        Assert.Equal(3, allEntries.Count);

        var dtcEntries = timeline.GetEntries(10, filterCategory: TimelineCategory.DtcEvent);
        Assert.Single(dtcEntries);
        Assert.Equal("DTC P0300 Detected", dtcEntries[0].Title);

        var warnings = timeline.GetEntries(10, minSeverity: TimelineSeverity.Warning);
        Assert.Single(warnings);
    }

    [Fact]
    public void UnifiedTimelineService_SearchQuery_FindsMatchingEntries()
    {
        var timeline = new UnifiedTimelineService();
        timeline.Record(TimelineCategory.CanFrame, "CAN ID 0x280", "RPM Signal update", TimelineSeverity.Trace, "CAN1", "00 12 34 56");
        timeline.Record(TimelineCategory.UdsExchange, "UDS ReadDataByIdentifier", "VIN Request", TimelineSeverity.Info, "UDS", "22 F1 90");

        var results = timeline.Search("VIN");
        Assert.Single(results);
        Assert.Equal("UDS ReadDataByIdentifier", results[0].Title);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. CAN REVERSE-ENGINEERING LAB TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CanReverseEngineering_IngestFrames_CalculatesEntropyAndTiming()
    {
        var svc = new CanReverseEngineeringService();

        for (int i = 0; i < 20; i++)
        {
            byte[] data = new byte[] { (byte)i, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE };
            var frame = new CanFrame(0, 0x280, 8, 0, data);
            svc.IngestFrame(frame);
        }

        var metrics = svc.GetMetricsForId(0x280);
        Assert.NotNull(metrics);
        Assert.Equal(20, metrics.MessageCount);
        Assert.True(metrics.PayloadEntropy > 0);
        Assert.True(metrics.ByteChangeCounts[0] > 0); // Byte 0 was changing
    }

    [Fact]
    public void CanReverseEngineering_CompareEvent_IdentifiesCorrelatedChanges()
    {
        var svc = new CanReverseEngineeringService();

        // Feed baseline frames
        for (int i = 0; i < 10; i++)
        {
            svc.IngestFrame(new CanFrame(0, 0x1A0, 8, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
        }

        svc.SetBaselineCapture();

        // Create event frames with high entropy (e.g. Throttle Applied)
        var eventFrames = new List<CanFrame>();
        for (int i = 0; i < 10; i++)
        {
            eventFrames.Add(new CanFrame(0, 0x1A0, 8, 0, new byte[] { (byte)i, 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99 }));
        }

        var comparison = svc.CompareEvent("Throttle Tip-In", eventFrames);
        Assert.NotNull(comparison);
        Assert.NotEmpty(comparison.CorrelatedChanges);
        Assert.Equal(0x1A0U, comparison.CorrelatedChanges[0].CanId);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. VEHICLE DIGITAL TWIN TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VehicleDigitalTwin_DefaultNodesInitialized_AndFaultInjectable()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        using var twin = new VehicleDigitalTwinService(pipeline);

        var nodes = twin.GetNodes();
        Assert.True(nodes.Count >= 8);

        var ecm = twin.GetNode("ECM");
        Assert.NotNull(ecm);
        Assert.Equal(0x7E0U, ecm.RequestCanId);

        // Inject fault
        twin.SetFault(new DigitalTwinFaultRequest
        {
            NodeId = "ECM",
            DtcCode = "P0171",
            SetDtcActive = true,
            VoltageOverride = 11.2
        });

        var faultedEcm = twin.GetNode("ECM");
        Assert.NotNull(faultedEcm);
        Assert.Contains("P0171", faultedEcm.ActiveDtcs);
        Assert.Equal(11.2, faultedEcm.Voltage);

        // Clear faults
        twin.ClearFaults("ECM");
        var restoredEcm = twin.GetNode("ECM");
        Assert.NotNull(restoredEcm);
        Assert.Empty(restoredEcm.ActiveDtcs);
        Assert.Equal(13.8, restoredEcm.Voltage);
    }

    [Fact]
    public async Task VehicleDigitalTwin_StartStopSimulation_RunsCleanly()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        using var twin = new VehicleDigitalTwinService(pipeline);

        twin.StartSimulation();
        Assert.True(twin.IsRunning);

        await Task.Delay(50);

        twin.StopSimulation();
        Assert.False(twin.IsRunning);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. HIL ARCHITECTURE TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void HilService_ChannelControlAndFaultInjection_Works()
    {
        var hil = new HilService();
        var fixture = hil.GetFixtureStatus();

        Assert.True(fixture.IsOnline);
        Assert.NotEmpty(fixture.Channels);

        // Set channel value
        hil.SetChannelValue("AIN_CRANK", 3500);
        var updated = hil.GetFixtureStatus();
        var crank = updated.Channels.FirstOrDefault(c => c.ChannelId == "AIN_CRANK");
        Assert.NotNull(crank);
        Assert.Equal(3500, crank.CurrentValue);

        // Inject fault
        hil.InjectChannelFault(new HilFaultInjectionCommand
        {
            ChannelId = "AIN_PEDAL1",
            InjectedState = HilChannelState.ShortToGround,
            OverrideValue = 0.0
        });

        var faulted = hil.GetFixtureStatus();
        var pedal = faulted.Channels.FirstOrDefault(c => c.ChannelId == "AIN_PEDAL1");
        Assert.NotNull(pedal);
        Assert.Equal(HilChannelState.ShortToGround, pedal.State);
        Assert.Equal(0.0, pedal.CurrentValue);

        // Toggle ignition
        hil.SetIgnition(false);
        var ignOff = hil.GetFixtureStatus();
        Assert.False(ignOff.Terminal15Active);

        // Reset
        hil.ResetAllChannels();
        var resetFixture = hil.GetFixtureStatus();
        Assert.True(resetFixture.Terminal15Active);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. ASAM & XCP PROTOCOL TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AsamProtocolService_A2lAndOdxData_Populated()
    {
        var asam = new AsamProtocolService();

        var a2l = asam.GetA2lDatabase();
        Assert.NotNull(a2l);
        Assert.NotEmpty(a2l.Measurements);
        Assert.NotEmpty(a2l.Characteristics);

        var nmot = a2l.Measurements.FirstOrDefault(m => m.Name == "nmot_w");
        Assert.NotNull(nmot);
        Assert.Equal("1/min", nmot.Unit);

        var odx = asam.GetOdxVariant();
        Assert.NotNull(odx);
        Assert.NotEmpty(odx.Services);
    }

    [Fact]
    public void AsamProtocolService_XcpConnectAndMemoryTransfer_Succeeds()
    {
        var asam = new AsamProtocolService();

        bool connected = asam.ConnectXcp(XcpTransportType.XcpOnCan);
        Assert.True(connected);

        var status = asam.GetXcpStatus();
        Assert.True(status.IsConnected);
        Assert.Equal(XcpTransportType.XcpOnCan, status.Transport);

        byte[] uploaded = asam.XcpShortUpload(0x800000, 8);
        Assert.Equal(8, uploaded.Length);

        bool downloaded = asam.XcpShortDownload(0x800000, new byte[] { 0x01, 0x02 });
        Assert.True(downloaded);

        asam.DisconnectXcp();
        var disconnectedStatus = asam.GetXcpStatus();
        Assert.False(disconnectedStatus.IsConnected);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 7. RESEARCH & GLOBAL KNOWLEDGE GRAPH TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ResearchKnowledgeService_KnowledgeGraphAndGlobalSearch_ReturnsMatches()
    {
        var svc = new ResearchKnowledgeService();

        var nodes = svc.GetGraphNodes();
        var edges = svc.GetGraphEdges();

        Assert.NotEmpty(nodes);
        Assert.NotEmpty(edges);

        // Search for MED17
        var results = svc.SearchGlobal("MED17");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("MED17", StringComparison.OrdinalIgnoreCase));

        // Search for TriCore
        var mcuResults = svc.SearchGlobal("TriCore");
        Assert.NotEmpty(mcuResults);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 8. AI ENGINEERING COPILOT TESTS
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AiCopilot_DtcQuery_ReturnsEvidenceAndAlternativeExplanation()
    {
        var copilot = new AiCopilotService();

        var response = await copilot.AnalyzeAsync(new CopilotQueryRequest
        {
            Domain = "DTC",
            UserPrompt = "Explain cylinder 1 misfire DTC P0301 with freeze frame data",
            EcuName = "ECM"
        });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Conclusion);
        Assert.NotEmpty(response.SupportingEvidence);
        Assert.NotEmpty(response.AlternativeExplanation);
        Assert.NotEmpty(response.VerificationProcedure);
        Assert.True(response.RequiresOperatorPhysicalVerification);
    }

    [Fact]
    public async Task AiCopilot_ChecksumQuery_ReturnsAlgorithmAndSafetyGuard()
    {
        var copilot = new AiCopilotService();

        var response = await copilot.AnalyzeAsync(new CopilotQueryRequest
        {
            Domain = "Checksum",
            UserPrompt = "What is the checksum algorithm for Bosch MED17.5.5 flash?",
            EcuName = "ECM"
        });

        Assert.NotNull(response);
        Assert.Contains("CRC", response.Conclusion, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(response.SupportingEvidence);
    }
}
