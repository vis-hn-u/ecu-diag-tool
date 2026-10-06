using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.Transport;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Hardware.Protocols.DoIp;
using CanEngineeringPlatform.Core.Hardware.Protocols.Ethernet;
using CanEngineeringPlatform.Core.Hardware.Protocols.FlexRay;
using CanEngineeringPlatform.Core.Hardware.Protocols.J1850;
using CanEngineeringPlatform.Core.Hardware.Protocols.J1939;
using CanEngineeringPlatform.Core.Hardware.Protocols.KLine;
using CanEngineeringPlatform.Core.Hardware.Protocols.Kwp2000;
using CanEngineeringPlatform.Core.Hardware.Protocols.Lin;
using CanEngineeringPlatform.Core.Hardware.Protocols.Oem;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class AutomotiveTransportAbstractionTests
{
    // ==========================================
    // 1. LIN Protocol Abstraction Tests
    // ==========================================
    [Theory]
    [InlineData(0x01, 0xC1)] // ID 1 -> P0=1, P1=1 -> PID 0xC1
    [InlineData(0x3C, 0x3C)] // ID 0x3C (Diagnostic Master Req)
    [InlineData(0x3D, 0x7D)] // ID 0x3D (Diagnostic Slave Resp)
    public void Lin_CalculatePid_ProducesStandardCompliantParity(byte id, byte expectedPid)
    {
        byte pid = LinFrame.CalculatePid(id);
        Assert.Equal(expectedPid, pid);
    }

    [Fact]
    public void Lin_CalculateChecksum_EnhancedAndClassic_Valid()
    {
        byte pid = 0xC1;
        byte[] payload = new byte[] { 0x11, 0x22, 0x33, 0x44 };

        byte classic = LinFrame.CalculateChecksum(pid, payload, LinChecksumType.Classic);
        byte enhanced = LinFrame.CalculateChecksum(pid, payload, LinChecksumType.Enhanced);

        Assert.NotEqual(classic, enhanced);

        // Verify with LinFrame constructor
        var frame = new LinFrame(0x01, payload, LinChecksumType.Enhanced, channel: 0);
        Assert.Equal(enhanced, frame.Checksum);
        Assert.Equal(LinFrameType.Unconditional, frame.FrameType);
        Assert.False(frame.HasError);
    }

    [Fact]
    public void Lin_DiagnosticFrames_IdentifiedCorrectly()
    {
        var masterReq = new LinFrame(0x3C, new byte[] { 0x01, 0x02 });
        var slaveResp = new LinFrame(0x3D, new byte[] { 0x03, 0x04 });

        Assert.Equal(LinFrameType.DiagnosticMasterRequest, masterReq.FrameType);
        Assert.Equal(LinFrameType.DiagnosticSlaveResponse, slaveResp.FrameType);
    }

    // ==========================================
    // 2. K-Line / ISO 9141-2 Tests
    // ==========================================
    [Fact]
    public void KLine_ChecksumCalculation_AndValidation()
    {
        // 0x68, 0x6A, 0xF1, 0x01, 0x00 -> Checksum should be modulo 256 sum
        byte[] raw = new byte[] { 0x68, 0x6A, 0xF1, 0x01, 0x00, 0x00 };
        byte calculated = KLineMessage.CalculateChecksum(raw, 0, 5);
        raw[5] = calculated;

        var msg = new KLineMessage(raw, channel: 0);
        Assert.True(msg.IsChecksumValid);
        Assert.Equal(calculated, msg.Checksum);
    }

    [Fact]
    public void KLine_Capabilities_ReportStandardValues()
    {
        var caps = new KLineCapabilities();
        Assert.Equal(AutomotiveProtocol.KLine, caps.Protocol);
        Assert.True(caps.Supports5BaudInit);
        Assert.True(caps.SupportsFastInit);
        Assert.Contains(10400u, caps.SupportedBaudRates);
    }

    // ==========================================
    // 3. KWP2000 / ISO 14230 Tests
    // ==========================================
    [Fact]
    public void Kwp2000_MessageHeaderFormat_AndSerialization()
    {
        byte serviceId = 0x1A; // ReadECUIdentification
        byte[] payload = new byte[] { 0x90 }; // VIN identifier

        var msg = new Kwp2000Message(
            serviceId: serviceId,
            payload: payload,
            targetAddress: 0x11,
            sourceAddress: 0xF1,
            physicalLayer: KwpPhysicalLayer.KLine
        );

        Assert.Equal(KwpHeaderFormat.TargetSourceWithLength, msg.Format);
        byte[] raw = msg.ToRawBytes();

        Assert.NotNull(raw);
        Assert.True(raw.Length >= 5);
        Assert.Equal(0x11, raw[1]); // Target
        Assert.Equal(0xF1, raw[2]); // Source
        Assert.Equal(serviceId, raw[3]);
        Assert.Equal(0x90, raw[4]);
    }

    // ==========================================
    // 4. SAE J1850 VPW / PWM Tests
    // ==========================================
    [Fact]
    public void J1850_FrameCrcCalculation_IsDeterministic()
    {
        byte[] header = new byte[] { 0x68, 0x6A, 0xF1 };
        byte[] data = new byte[] { 0x01, 0x00 };

        byte crc = J1850Frame.CalculateCrc(header, data);
        var frame = new J1850Frame(J1850Variant.Vpw_10_4K, header, data);

        Assert.Equal(crc, frame.Crc);
        Assert.True(frame.IsCrcValid);
        Assert.False(frame.HasInFrameResponse);
    }

    // ==========================================
    // 5. Automotive Ethernet Tests
    // ==========================================
    [Fact]
    public void AutomotiveEthernet_Frame_WithVlanTagging()
    {
        byte[] dst = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x01 };
        byte[] src = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 };
        byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var frame = new AutomotiveEthernetFrame(
            destinationMac: dst,
            sourceMac: src,
            etherType: 0x0800,
            payload: payload,
            vlanId: 100,
            vlanPriority: 5,
            phyType: EthernetPhyType.BaseT1_100,
            portIndex: 0
        );

        Assert.Equal((ushort)100, frame.VlanId!.Value);
        Assert.Equal((byte)5, frame.VlanPriority!.Value);
        Assert.Equal(EthernetPhyType.BaseT1_100, frame.PhyType);
        Assert.Equal(4, frame.Payload.Length);
    }

    // ==========================================
    // 6. DoIP / ISO 13400 Tests
    // ==========================================
    [Fact]
    public void DoIp_DiagnosticMessage_Serialization_ValidHeader()
    {
        byte[] udsPayload = new byte[] { 0x22, 0xF1, 0x90 }; // Read VIN
        var msg = DoIpMessage.CreateDiagnosticMessage(
            sourceAddress: 0x0E00,
            targetAddress: 0x0001,
            udsPayload: udsPayload
        );

        Assert.Equal(0x02, msg.ProtocolVersion);
        Assert.Equal(0xFD, msg.InverseProtocolVersion);
        Assert.Equal(DoIpPayloadType.DiagnosticMessage, msg.PayloadType);

        byte[] raw = msg.ToRawBytes();
        Assert.NotNull(raw);
        // Header is 8 bytes + 4 bytes logical addresses + 3 bytes UDS = 15 bytes
        Assert.Equal(15, raw.Length);
        Assert.Equal(0x02, raw[0]); // Version
        Assert.Equal(0xFD, raw[1]); // Inverse version
        Assert.Equal(0x80, raw[2]); // Payload type MSB
        Assert.Equal(0x01, raw[3]); // Payload type LSB (0x8001)
    }

    // ==========================================
    // 7. FlexRay (ISO 17458) Tests
    // ==========================================
    [Fact]
    public void FlexRay_Frame_HeaderCrcAndPayloadCalculation()
    {
        byte[] payload = new byte[32];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;

        var frame = new FlexRayFrame(
            frameId: 42,
            payload: payload,
            cycleCount: 15,
            channel: FlexRayChannel.ChannelA
        );

        Assert.Equal(42, frame.FrameId);
        Assert.Equal(16, frame.PayloadLengthWords); // 32 bytes / 2 = 16 words
        Assert.Equal(32, frame.PayloadLengthBytes);
        Assert.Equal(15, frame.CycleCount);
        Assert.False(frame.NullFrameIndicator);
    }

    // ==========================================
    // 8. SAE J1939 / J1939-22 Tests
    // ==========================================
    [Fact]
    public void J1939_PgnExtraction_AndRoundTripCanIdSynthesis()
    {
        // PGN 61444 (0x00F004) Electronic Engine Controller 1 (EEC1)
        // Priority: 3, DP: 0, PF: 0xF0, PS: 0x04, SA: 0x00
        uint expectedCanId = 0x0CF00400;

        var msg = J1939Message.FromCanId(expectedCanId, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });

        Assert.Equal(61444u, msg.Pgn);
        Assert.Equal(3, msg.Priority);
        Assert.Equal(0, msg.DataPage);
        Assert.Equal(0xF0, msg.PduFormat);
        Assert.Equal(0x04, msg.PduSpecific);
        Assert.Equal(0x00, msg.SourceAddress);
        Assert.True(msg.IsBroadcast);

        uint synthesizedCanId = msg.ToCanId();
        Assert.Equal(expectedCanId, synthesizedCanId);
    }

    [Fact]
    public void J1939_Pdu1_DestinationSpecific_ExtractsCorrectAddresses()
    {
        // PDU1: PF < 240. E.g. PGN 0xEA00 (Request), Priority 6, Target 0x11, Source 0xF9
        // CanId: Priority 6 (0x18), DP 0, PF 0xEA, PS 0x11 (DA), SA 0xF9
        uint pdu1CanId = 0x18EA11F9;

        var msg = J1939Message.FromCanId(pdu1CanId, new byte[] { 0x00, 0xEE, 0x00 });

        Assert.Equal(0xEA00u, msg.Pgn);
        Assert.Equal(0x11, msg.DestinationAddress);
        Assert.Equal(0xF9, msg.SourceAddress);
        Assert.False(msg.IsBroadcast);

        uint roundTrip = msg.ToCanId();
        Assert.Equal(pdu1CanId, roundTrip);
    }

    // ==========================================
    // 9. Multi-Protocol Hardware Manager Registration
    // ==========================================
    [Fact]
    public void HardwareManager_SupportsRegistering_MultiProtocolTransports()
    {
        var hwManager = new HardwareManager();

        // 1. Register CAN transport
        var simCan = new SimulationTransport();
        hwManager.RegisterTransport(simCan);

        // 2. Register mock LIN transport
        var linMock = new MockLinTransport("lin-adapter-1", "Bench LIN Adapter");
        hwManager.RegisterAutomotiveTransport(linMock);

        // Verify querying
        var allAuto = hwManager.GetAllAutomotiveTransports();
        Assert.Equal(2, allAuto.Count);

        var linTransports = hwManager.GetTransportsByProtocol<ILinTransport>(AutomotiveProtocol.Lin);
        Assert.Single(linTransports);
        Assert.Equal("lin-adapter-1", linTransports[0].TransportId);

        var canTransports = hwManager.GetAllTransports();
        Assert.Single(canTransports);
        Assert.Equal("virtual-simulation", canTransports[0].TransportId);
    }

    // ==========================================
    // 10. Transport-Agnostic Diagnostics
    // ==========================================
    [Fact]
    public async Task DiagnosticSession_WorksWith_IDiagnosticTransport()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTpRouter = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTpRouter);
        var mockDiagTransport = new MockDiagnosticTransport();

        using var sessionService = new DiagnosticSessionService(
            pipeline: pipeline,
            isoTpRouter: isoTpRouter,
            ecuSimulator: ecuSim,
            hardwareManager: null,
            diagnosticTransport: mockDiagTransport
        );

        // Send non-simulated request through the session
        byte[] requestData = new byte[] { 0x22, 0xF1, 0x90 }; // UDS Read DID 0xF190
        var exchange = await sessionService.SendRequestAsync(0x7E0, requestData, ecuName: "ECM", isSimulation: false);

        Assert.NotNull(exchange);
        Assert.Equal("ECM", exchange.EcuName);
        Assert.True(mockDiagTransport.LastPayloadSent != null);
        Assert.Equal(requestData, mockDiagTransport.LastPayloadSent);

        // Simulate diagnostic response arrival via IDiagnosticTransport
        byte[] responseData = new byte[] { 0x62, 0xF1, 0x90, 0x31, 0x32, 0x33 }; // Positive response
        mockDiagTransport.SimulateResponse(0x7E8, responseData);

        var recent = sessionService.GetRecentExchanges();
        var completedExchange = recent.FirstOrDefault(e => e.ResponseCanId == 0x7E8);
        Assert.NotNull(completedExchange);
        Assert.True(completedExchange.IsSuccess);
        Assert.Equal("UDS", completedExchange.Protocol);
    }

    // ==========================================
    // 11. Existing CAN Hardware Preservation
    // ==========================================
    [Fact]
    public void ExistingTransports_PreservedAsAutomotiveTransports()
    {
        var esp = new Esp8266Transport();
        var ws = new WaveshareCanFdTransport();
        var sim = new SimulationTransport();

        // Verify all implement IAutomotiveTransport
        Assert.IsAssignableFrom<IAutomotiveTransport>(esp);
        Assert.IsAssignableFrom<IAutomotiveTransport>(ws);
        Assert.IsAssignableFrom<IAutomotiveTransport>(sim);

        // Verify protocol is CAN
        Assert.Equal(AutomotiveProtocol.Can, ((IAutomotiveTransport)esp).Protocol);
        Assert.Equal(AutomotiveProtocol.Can, ((IAutomotiveTransport)ws).Protocol);
        Assert.Equal(AutomotiveProtocol.Can, ((IAutomotiveTransport)sim).Protocol);
    }
}

// ==========================================
// Test Doubles
// ==========================================
#pragma warning disable CS0067
internal class MockLinTransport : ILinTransport
{
    public string TransportId { get; }
    public string Name { get; }
    public LinCapabilities Capabilities { get; } = new();
    public TransportState State { get; private set; } = TransportState.Connected;

    public event Action<LinFrame>? OnLinFrameReceived;
    public event Action<AutomotiveTransportStatus>? OnAutomotiveStatusChanged;

    public MockLinTransport(string id, string name)
    {
        TransportId = id;
        Name = name;
    }

    public Task<bool> ConnectAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task DisconnectAsync() => Task.CompletedTask;
    public ValueTask<bool> SendFrameAsync(LinFrame frame, System.Threading.CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    public AutomotiveTransportStatus GetAutomotiveStatus() => new()
    {
        TransportId = TransportId,
        Name = Name,
        Protocol = AutomotiveProtocol.Lin,
        State = State
    };
    public void Dispose() { }
}

internal class MockDiagnosticTransport : IDiagnosticTransport
{
    public string TransportId => "mock-diagnostic-transport";
    public AutomotiveProtocol Protocol => AutomotiveProtocol.DoIp;
    public bool IsConnected => true;
    public byte[]? LastPayloadSent { get; private set; }

    public event Action<DiagnosticPayloadReceivedEventArgs>? OnPayloadReceived;

    public Task<bool> SendDiagnosticPayloadAsync(uint targetAddress, byte[] payload, System.Threading.CancellationToken cancellationToken = default)
    {
        LastPayloadSent = payload;
        return Task.FromResult(true);
    }

    public void SimulateResponse(uint sourceAddress, byte[] payload)
    {
        OnPayloadReceived?.Invoke(new DiagnosticPayloadReceivedEventArgs
        {
            SourceAddress = sourceAddress,
            Payload = payload,
            Protocol = Protocol,
            TimestampUs = 123456
        });
    }

    public void Dispose() { }
}
#pragma warning restore CS0067
