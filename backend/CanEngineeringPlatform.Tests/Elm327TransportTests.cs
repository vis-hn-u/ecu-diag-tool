using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Hardware.Transports.Elm327;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Tests;

public class Elm327TransportTests
{
    [Fact]
    public async Task Handshake_IdentifiesAdapterAndReadsVoltage()
    {
        var simChannel = new SimulatedElmChannel
        {
            DeviceVersion = "ELM327 v2.2",
            DeviceDescription = "OBDLink MX+ Bluetooth",
            BatteryVoltage = 12.8,
            CurrentProtocol = Elm327Protocol.Iso15765_4_Can_11Bit_500k
        };

        using var elm = new Elm327Transport(simChannel);
        bool connected = await elm.ConnectAsync();

        Assert.True(connected);
        Assert.True(elm.GetStatus().IsConnected);
        Assert.Equal(Elm327ConnectionState.Connected, elm.ConnectionState);
        Assert.Equal("ELM327 v2.2", elm.AdapterInfo.FirmwareVersion);
        Assert.Equal("OBDLink MX+ Bluetooth", elm.AdapterInfo.Description);
        Assert.Equal(12.8, elm.AdapterInfo.BatteryVoltage);
        Assert.Equal(Elm327Protocol.Iso15765_4_Can_11Bit_500k, elm.AdapterInfo.ActiveProtocol);
        Assert.True(elm.ElmCapabilities.SupportsCan);
        Assert.True(elm.ElmCapabilities.SupportsCustomHeaders);
        Assert.True(elm.ElmCapabilities.SupportsAdaptiveTiming);
    }

    [Theory]
    [InlineData(Elm327Protocol.Iso15765_4_Can_11Bit_500k, "6", true)]
    [InlineData(Elm327Protocol.Iso15765_4_Can_29Bit_500k, "7", true)]
    [InlineData(Elm327Protocol.Iso15765_4_Can_11Bit_250k, "8", true)]
    [InlineData(Elm327Protocol.Iso15765_4_Can_29Bit_250k, "9", true)]
    [InlineData(Elm327Protocol.Iso9141_2, "3", false)]
    [InlineData(Elm327Protocol.Iso14230_4_Kwp_Fast, "5", false)]
    [InlineData(Elm327Protocol.SaeJ1850Vpw, "2", false)]
    [InlineData(Elm327Protocol.SaeJ1850Pwm, "1", false)]
    public async Task ProtocolDetection_SupportsAllStandardProtocolFamilies(Elm327Protocol protocol, string code, bool isCan)
    {
        var simChannel = new SimulatedElmChannel
        {
            CurrentProtocol = protocol
        };

        using var elm = new Elm327Transport(simChannel);
        elm.Configure(
            nominalBaudRate: protocol == Elm327Protocol.Iso15765_4_Can_11Bit_250k ? 250000U : 500000U,
            protocol: protocol);

        bool connected = await elm.ConnectAsync();
        Assert.True(connected);
        Assert.Equal(protocol, elm.AdapterInfo.ActiveProtocol);
        Assert.Equal(isCan, elm.AdapterInfo.ActiveProtocol.IsCanProtocol());
        Assert.Equal(code, protocol.ToProtocolCode());
    }

    [Fact]
    public async Task CommandFormattingAndExecution_ReturnsStructuredResponse()
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);
        await elm.ConnectAsync();

        // Query Engine RPM (Mode 01 PID 0x0C)
        var response = await elm.ExecuteElmCommandAsync("010C");

        Assert.True(response.Success);
        Assert.Equal(Elm327ResponseType.Data, response.Type);
        Assert.NotEmpty(response.Lines);
        Assert.Contains(response.Lines, l => l.Contains("41 0C"));
    }

    [Theory]
    [InlineData("?", Elm327ResponseType.UnknownCommand)]
    [InlineData("NO DATA", Elm327ResponseType.NoData)]
    [InlineData("CAN ERROR", Elm327ResponseType.CanError)]
    [InlineData("BUS BUSY", Elm327ResponseType.BusBusy)]
    [InlineData("BUFFER FULL", Elm327ResponseType.BufferFull)]
    [InlineData("DATA ERROR", Elm327ResponseType.DataError)]
    [InlineData("STOPPED", Elm327ResponseType.Stopped)]
    public async Task MalformedResponseHandling_ClassifiesErrorsWithoutCrashing(string injectedError, Elm327ResponseType expectedType)
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);
        await elm.ConnectAsync();

        simChannel.InjectedErrorResponse = injectedError;
        var response = await elm.ExecuteElmCommandAsync("0100");

        Assert.False(response.Success);
        Assert.Equal(expectedType, response.Type);
        Assert.NotNull(response.ErrorMessage);
        Assert.NotNull(elm.GetStatus().LastError);
    }

    [Fact]
    public async Task TimeoutHandling_ReturnsCleanTimeoutResponse()
    {
        var simChannel = new SimulatedElmChannel
        {
            InjectTimeout = true
        };

        using var elm = new Elm327Transport(simChannel);
        await elm.ConnectAsync();

        var response = await elm.ExecuteElmCommandAsync("0100", TimeSpan.FromMilliseconds(50));

        Assert.False(response.Success);
        Assert.Equal(Elm327ResponseType.Timeout, response.Type);
        Assert.Contains("Timeout", response.ErrorMessage);
    }

    [Fact]
    public void CanonicalConversion_ConvertsStandard11BitAnd29BitCan()
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);

        CanFrame? standardFrame = null;
        CanFrame? extendedFrame = null;
        elm.OnFrameReceived += f =>
        {
            if (f.IsExtended) extendedFrame = f;
            else standardFrame = f;
        };

        // 11-bit CAN frame
        bool s1 = elm.ProcessIncomingElmLine("7E8 06 41 00 BE 3F B8 11");
        Assert.True(s1);
        Assert.NotNull(standardFrame);
        Assert.Equal(0x7E8U, standardFrame.Value.Id);
        Assert.False(standardFrame.Value.IsExtended);
        Assert.Equal(7, standardFrame.Value.ActualLength);
        Assert.Equal(0x06, standardFrame.Value.Data[0]);
        Assert.Equal(0x41, standardFrame.Value.Data[1]);
        Assert.Equal(CanDirection.Rx, standardFrame.Value.Direction);

        // 29-bit CAN frame
        bool s2 = elm.ProcessIncomingElmLine("18DAF110 08 49 02 01 00 00 00 00");
        Assert.True(s2);
        Assert.NotNull(extendedFrame);
        Assert.Equal(0x18DAF110U, extendedFrame.Value.Id);
        Assert.True(extendedFrame.Value.IsExtended);
        Assert.Equal(8, extendedFrame.Value.ActualLength);
        Assert.Equal(0x49, extendedFrame.Value.Data[1]);
    }

    [Fact]
    public void LegacyProtocolConversion_MapsToDiagnosticFrames()
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);

        CanFrame? captured = null;
        elm.OnFrameReceived += f => captured = f;

        // In non-CAN mode (K-Line / J1850), response has no CAN header: "41 0C 1F 40"
        bool success = elm.ProcessIncomingElmLine("41 0C 1F 40");

        Assert.True(success);
        Assert.NotNull(captured);
        Assert.Equal(0x7E8U, captured.Value.Id); // Mapped to canonical ECU diagnostic ID
        Assert.False(captured.Value.IsExtended);
        Assert.Equal(0x04, captured.Value.Data[0]); // ISO-TP SF Length = 4
        Assert.Equal(0x41, captured.Value.Data[1]); // Positive Mode 01
        Assert.Equal(0x0C, captured.Value.Data[2]); // Engine RPM PID
    }

    [Fact]
    public async Task PipelineRouting_InjectsFramesIntoExistingCanPipeline()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);
        using var hwManager = new HardwareManager(pipeline, new ICanTransport[] { elm });

        await hwManager.SetActiveTransportAsync("elm327-adapter");

        var receivedFrames = new List<CanFrame>();
        pipeline.OnFrameReceived += f => receivedFrames.Add(f);

        // Feed an ELM327 line
        elm.ProcessIncomingElmLine("7E8 04 41 0C 1F 40");

        // Wait briefly for pipeline channel to process
        await Task.Delay(100);

        Assert.NotEmpty(receivedFrames);
        var frame = receivedFrames.First();
        Assert.Equal(0x7E8U, frame.Id);
        Assert.Equal(0x41, frame.Data[1]);
    }

    [Fact]
    public async Task DisconnectAndReconnect_RestoresCleanState()
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);

        Assert.Equal(Elm327ConnectionState.Disconnected, elm.ConnectionState);
        Assert.False(elm.GetStatus().IsConnected);

        await elm.ConnectAsync();
        Assert.Equal(Elm327ConnectionState.Connected, elm.ConnectionState);
        Assert.True(elm.GetStatus().IsConnected);

        await elm.DisconnectAsync();
        Assert.Equal(Elm327ConnectionState.Disconnected, elm.ConnectionState);
        Assert.False(elm.GetStatus().IsConnected);

        await elm.ConnectAsync();
        Assert.Equal(Elm327ConnectionState.Connected, elm.ConnectionState);
        Assert.True(elm.GetStatus().IsConnected);
    }

    [Fact]
    public async Task SafetyAudit_RejectsCanFdAndMaintainsReadonlyIntegrity()
    {
        var simChannel = new SimulatedElmChannel();
        using var elm = new Elm327Transport(simChannel);
        await elm.ConnectAsync();

        // 1. CAN FD frame rejection
        byte[] fdData = new byte[64];
        var fdFrame = CanFrame.CreateCanFd(0x123, fdData, channel: 1);
        bool sent = await elm.SendFrameAsync(fdFrame);
        Assert.False(sent, "ELM327 must strictly reject CAN FD frames.");

        // 2. Large DLC rejection
        var largeFrame = new CanFrame { Id = 0x123, Dlc = 12, Data = new byte[12] };
        bool sentLarge = await elm.SendFrameAsync(largeFrame);
        Assert.False(sentLarge, "ELM327 must reject DLC > 8.");
    }
}
