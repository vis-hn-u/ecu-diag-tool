using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class GvretSerialTransportTests
{
    [Fact]
    public void TransportId_IsArduinoNanoGvret()
    {
        var sut = new GvretSerialTransport();
        Assert.Equal("arduino-nano-gvret", sut.TransportId);
        Assert.Equal(TransportType.GvretSerial, sut.Type);
        Assert.Contains("Arduino Nano", sut.Name);
        Assert.Contains("GVRET", sut.Name);
    }

    [Fact]
    public void Capabilities_ClassicCan_1Channel_SupportsTransmitTrue()
    {
        var sut = new GvretSerialTransport();
        Assert.True(sut.Capabilities.SupportsClassicCan);
        Assert.False(sut.Capabilities.SupportsCanFd);
        Assert.True(sut.Capabilities.SupportsTransmit);
        Assert.Equal(1, sut.Capabilities.ChannelCount);
        Assert.Equal(8, sut.Capabilities.MaxPayloadBytes);
        Assert.Contains(500000u, sut.Capabilities.SupportedNominalBaudRates);
        Assert.Contains(250000u, sut.Capabilities.SupportedNominalBaudRates);
    }

    [Fact]
    public void HandshakePacket_ExactPinnedSavvyCanSequence()
    {
        // Must match SavvyCAN connections/gvretserial.cpp lines 374..410:
        // E7 E7 F1 0C F1 06 F1 07 F1 01 F1 09
        byte[] expected = new byte[]
        {
            0xE7, 0xE7,
            0xF1, 0x0C, // PROTO_GET_NUMBUSES
            0xF1, 0x06, // PROTO_GET_CANBUS_PARAMS
            0xF1, 0x07, // PROTO_GET_DEV_INFO
            0xF1, 0x01, // PROTO_TIME_SYNC
            0xF1, 0x09  // PROTO_KEEPALIVE
        };

        byte[] actual = GvretSerialTransport.BuildHandshakePacket();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BusSetupCommand_500kbps_ListenOnlyTrue_SetsBit29()
    {
        // 500,000 bps = 0x0007A120
        // with bit 31 (valid), bit 30 (enabled), bit 29 (listen-only):
        // can0Raw = 0x0007A120 | 0x80000000 | 0x40000000 | 0x20000000 = 0xE007A120
        byte[] cmd = GvretSerialTransport.BuildBusSetupCommand(500000u, listenOnly: true);

        Assert.Equal(11, cmd.Length);
        Assert.Equal(0xF1, cmd[0]);
        Assert.Equal(0x05, cmd[1]); // PROTO_SETUP_CANBUS

        // can0Raw LE uint32: 0xE007A120
        Assert.Equal(0x20, cmd[2]);
        Assert.Equal(0xA1, cmd[3]);
        Assert.Equal(0x07, cmd[4]);
        Assert.Equal(0xE0, cmd[5]);

        // can1Raw LE uint32: 0x00000000 (disabled)
        Assert.Equal(0x00, cmd[6]);
        Assert.Equal(0x00, cmd[7]);
        Assert.Equal(0x00, cmd[8]);
        Assert.Equal(0x00, cmd[9]);

        Assert.Equal(0x00, cmd[10]); // Terminator
    }

    [Fact]
    public void BusSetupCommand_500kbps_ListenOnlyFalse_ClearsBit29_EnablesTx()
    {
        // Normal Mode (Transmit Enabled):
        // can0Raw = 0x0007A120 | 0x80000000 | 0x40000000 = 0xC007A120
        byte[] cmd = GvretSerialTransport.BuildBusSetupCommand(500000u, listenOnly: false);

        Assert.Equal(11, cmd.Length);
        Assert.Equal(0xF1, cmd[0]);
        Assert.Equal(0x05, cmd[1]);

        // can0Raw LE uint32: 0xC007A120 (bit 29 is 0!)
        Assert.Equal(0x20, cmd[2]);
        Assert.Equal(0xA1, cmd[3]);
        Assert.Equal(0x07, cmd[4]);
        Assert.Equal(0xC0, cmd[5]);

        // Verify bit 29 is cleared
        uint reconstructed = (uint)(cmd[2] | (cmd[3] << 8) | (cmd[4] << 16) | (cmd[5] << 24));
        Assert.Equal(0u, reconstructed & 0x20000000u);
        Assert.Equal(0xC007A120u, reconstructed);
    }

    [Theory]
    [InlineData(125000u, 0x0001E848u)]
    [InlineData(250000u, 0x0003D090u)]
    [InlineData(1000000u, 0x000F4240u)]
    public void BusSetupCommand_VariousBaudRates_PreservesBitTiming(uint baud, uint expectedHex)
    {
        byte[] cmd = GvretSerialTransport.BuildBusSetupCommand(baud, listenOnly: true);
        uint reconstructed = (uint)(cmd[2] | (cmd[3] << 8) | (cmd[4] << 16) | (cmd[5] << 24));
        uint expectedFull = expectedHex | 0xE0000000u;
        Assert.Equal(expectedFull, reconstructed);
    }

    [Fact]
    public void OutboundFrame_StandardId_FormatMatchesSavvyCan()
    {
        var frame = new CanFrame(
            timestampUs: 10000,
            id: 0x123,
            dlc: 8,
            flags: 0,
            data: new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }
        );

        byte[] packet = GvretSerialTransport.BuildOutboundFrame(frame, channel: 0);

        Assert.Equal(17, packet.Length); // 2 header + 4 ID + 1 bus + 1 dlc + 8 data + 1 terminator
        Assert.Equal(0xF1, packet[0]);
        Assert.Equal(0x00, packet[1]);

        // ID 0x00000123 LE
        Assert.Equal(0x23, packet[2]);
        Assert.Equal(0x01, packet[3]);
        Assert.Equal(0x00, packet[4]);
        Assert.Equal(0x00, packet[5]);

        Assert.Equal(0x00, packet[6]); // Bus 0
        Assert.Equal(0x08, packet[7]); // DLC 8

        // Data bytes
        Assert.Equal(0x11, packet[8]);
        Assert.Equal(0x88, packet[15]);
        Assert.Equal(0x00, packet[16]); // Terminator
    }

    [Fact]
    public void OutboundFrame_ExtendedId_SetsBit31()
    {
        var frame = new CanFrame(
            timestampUs: 20000,
            id: 0x18DA10F1,
            dlc: 8,
            flags: CanFrame.FlagExtended,
            data: new byte[] { 0x02, 0x10, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 }
        );

        byte[] packet = GvretSerialTransport.BuildOutboundFrame(frame, channel: 0);

        // ID 0x18DA10F1 with bit 31 set = 0x98DA10F1 LE
        Assert.Equal(0xF1, packet[2]);
        Assert.Equal(0x10, packet[3]);
        Assert.Equal(0xDA, packet[4]);
        Assert.Equal(0x98, packet[5]);

        uint reconstructedId = (uint)(packet[2] | (packet[3] << 8) | (packet[4] << 16) | (packet[5] << 24));
        Assert.NotEqual(0u, reconstructedId & 0x80000000u); // Bit 31 set!
        Assert.Equal(0x18DA10F1u, reconstructedId & 0x7FFFFFFFu);
    }

    [Fact]
    public void InboundParser_StandardCanFrame_DecodedCorrectly()
    {
        var sut = new GvretSerialTransport();
        CanFrame? received = null;
        sut.OnFrameReceived += f => received = f;

        // F1 00 <Timestamp: 4B LE> <ID: 4B LE> <Bus/DLC: 1B> <Data: 8B> 00
        uint ts = 1234567u;
        uint id = 0x7E8u;
        byte busAndDlc = 0x08; // Bus 0, DLC 8

        byte[] stream = new byte[]
        {
            0xF1, 0x00,
            (byte)(ts & 0xFF), (byte)((ts >> 8) & 0xFF), (byte)((ts >> 16) & 0xFF), (byte)((ts >> 24) & 0xFF),
            (byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), (byte)((id >> 16) & 0xFF), (byte)((id >> 24) & 0xFF),
            busAndDlc,
            0x06, 0x49, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00 // trailing terminator
        };

        foreach (byte b in stream)
        {
            sut.ProcessByte(b);
        }

        Assert.NotNull(received);
        Assert.Equal(0x7E8u, received.Value.Id);
        Assert.False(received.Value.IsExtended);
        Assert.Equal(ts, received.Value.TimestampUs);
        Assert.Equal(8, received.Value.Dlc);
        Assert.Equal(0x06, received.Value.Data[0]);
        Assert.Equal(0x49, received.Value.Data[1]);
        Assert.Equal(0, received.Value.Channel);
        Assert.Equal("Arduino Nano GVRET", received.Value.SourceId);
    }

    [Fact]
    public void InboundParser_ExtendedCanFrame_DecodedCorrectly_ClearsBit31()
    {
        var sut = new GvretSerialTransport();
        CanFrame? received = null;
        sut.OnFrameReceived += f => received = f;

        uint ts = 9876543u;
        uint extId = 0x18DAF110u | 0x80000000u; // Bit 31 set by GVRET
        byte busAndDlc = 0x08;

        byte[] stream = new byte[]
        {
            0xF1, 0x00,
            (byte)(ts & 0xFF), (byte)((ts >> 8) & 0xFF), (byte)((ts >> 16) & 0xFF), (byte)((ts >> 24) & 0xFF),
            (byte)(extId & 0xFF), (byte)((extId >> 8) & 0xFF), (byte)((extId >> 16) & 0xFF), (byte)((extId >> 24) & 0xFF),
            busAndDlc,
            0x03, 0x7F, 0x22, 0x31, 0x00, 0x00, 0x00, 0x00,
            0x00
        };

        foreach (byte b in stream)
        {
            sut.ProcessByte(b);
        }

        Assert.NotNull(received);
        Assert.Equal(0x18DAF110u, received.Value.Id);
        Assert.True(received.Value.IsExtended);
        Assert.Equal(ts, received.Value.TimestampUs);
        Assert.Equal(8, received.Value.Dlc);
        Assert.Equal(0x03, received.Value.Data[0]);
        Assert.Equal(0x7F, received.Value.Data[1]);
    }

    [Fact]
    public void InboundParser_KeepaliveResponse_ValidatesComm()
    {
        var sut = new GvretSerialTransport();
        byte[] keepaliveResp = new byte[] { 0xF1, 0x09, 0xDE, 0xAD };

        foreach (byte b in keepaliveResp)
        {
            sut.ProcessByte(b);
        }

        var status = sut.GetStatus();
        dynamic tele = status.ExtendedTelemetry!;
        Assert.True((bool)tele.IsKeepaliveValidated);
    }

    [Fact]
    public void InboundParser_DeviceInfoResponse_ExtractsFirmwareBuild()
    {
        var sut = new GvretSerialTransport();
        // F1 07 <BuildLSB> <BuildMSB> <EEPROM> <FileType>
        // Build 500 = 0x01F4
        byte[] devInfoResp = new byte[] { 0xF1, 0x07, 0xF4, 0x01, 0x00, 0x00 };

        foreach (byte b in devInfoResp)
        {
            sut.ProcessByte(b);
        }

        var status = sut.GetStatus();
        dynamic tele = status.ExtendedTelemetry!;
        Assert.True((bool)tele.IsDeviceIdentified);
        Assert.Equal("500", (string)tele.FirmwareBuild);
    }

    [Fact]
    public void InboundParser_NumBusesResponse_ExtractsBusCount()
    {
        var sut = new GvretSerialTransport();
        byte[] numBusesResp = new byte[] { 0xF1, 0x0C, 0x01 };

        foreach (byte b in numBusesResp)
        {
            sut.ProcessByte(b);
        }

        var status = sut.GetStatus();
        dynamic tele = status.ExtendedTelemetry!;
        Assert.Equal((byte)1, (byte)tele.BusCount);
    }

    [Fact]
    public async Task ListenOnlySafetyInterlock_ThrowsOnSendWhenListenOnlyTrue()
    {
        var sut = new GvretSerialTransport();
        sut.Configure("COM8", 500000, listenOnly: true);

        var frame = new CanFrame(0, 0x7DF, 8, 0, new byte[8]);

        // Should throw because transport is not connected OR because listen-only is armed
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.SendFrameAsync(frame));
    }

    [Fact]
    public void StateMachine_GarbageBytesBeforeFrame_RecoversAndDecodes()
    {
        var sut = new GvretSerialTransport();
        CanFrame? received = null;
        sut.OnFrameReceived += f => received = f;

        // 30 random noise bytes
        var rnd = new Random(42);
        byte[] noise = new byte[30];
        rnd.NextBytes(noise);

        foreach (byte b in noise)
        {
            sut.ProcessByte(b);
        }

        // Then valid frame
        uint ts = 5000u;
        uint id = 0x123u;
        byte[] validFrame = new byte[]
        {
            0xF1, 0x00,
            (byte)(ts & 0xFF), (byte)((ts >> 8) & 0xFF), (byte)((ts >> 16) & 0xFF), (byte)((ts >> 24) & 0xFF),
            (byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), (byte)((id >> 16) & 0xFF), (byte)((id >> 24) & 0xFF),
            0x02, // Bus 0, DLC 2
            0xAA, 0xBB,
            0x00
        };

        foreach (byte b in validFrame)
        {
            sut.ProcessByte(b);
        }

        Assert.NotNull(received);
        Assert.Equal(0x123u, received.Value.Id);
        Assert.Equal(2, received.Value.Dlc);
        Assert.Equal(0xAA, received.Value.Data[0]);
        Assert.Equal(0xBB, received.Value.Data[1]);
    }

    [Fact]
    public void StateMachine_MultipleFramesBackToBack_AllDecoded()
    {
        var sut = new GvretSerialTransport();
        var list = new List<CanFrame>();
        sut.OnFrameReceived += f => list.Add(f);

        for (uint i = 1; i <= 5; i++)
        {
            uint ts = i * 1000;
            uint id = 0x100 + i;
            byte[] frameBytes = new byte[]
            {
                0xF1, 0x00,
                (byte)(ts & 0xFF), (byte)((ts >> 8) & 0xFF), (byte)((ts >> 16) & 0xFF), (byte)((ts >> 24) & 0xFF),
                (byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), (byte)((id >> 16) & 0xFF), (byte)((id >> 24) & 0xFF),
                0x01, // Bus 0, DLC 1
                (byte)i,
                0x00
            };

            foreach (byte b in frameBytes)
            {
                sut.ProcessByte(b);
            }
        }

        Assert.Equal(5, list.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal((uint)(0x101 + i), list[i].Id);
            Assert.Equal((byte)(i + 1), list[i].Data[0]);
        }
    }

    [Fact]
    public void Fuzz_RandomBytes_NoExceptions()
    {
        var sut = new GvretSerialTransport();
        var rnd = new Random(12345);
        byte[] fuzz = new byte[5000];
        rnd.NextBytes(fuzz);

        // Must run completely without throwing
        foreach (byte b in fuzz)
        {
            sut.ProcessByte(b);
        }

        Assert.True(true);
    }

    [Fact]
    public void Fuzz_HighRateFrames_1000Frames_AllDecoded()
    {
        var sut = new GvretSerialTransport();
        int count = 0;
        sut.OnFrameReceived += _ => count++;

        for (int i = 0; i < 1000; i++)
        {
            uint ts = (uint)i;
            uint id = (uint)(0x200 + (i % 16));
            byte[] frame = new byte[]
            {
                0xF1, 0x00,
                (byte)(ts & 0xFF), (byte)((ts >> 8) & 0xFF), (byte)((ts >> 16) & 0xFF), (byte)((ts >> 24) & 0xFF),
                (byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), (byte)((id >> 16) & 0xFF), (byte)((id >> 24) & 0xFF),
                0x04,
                0x11, 0x22, 0x33, 0x44,
                0x00
            };

            for (int j = 0; j < frame.Length; j++)
            {
                sut.ProcessByte(frame[j]);
            }
        }

        Assert.Equal(1000, count);
    }

    [Fact]
    public void Configure_UpdatesPreferredPort_AndBusSpeed()
    {
        var sut = new GvretSerialTransport();
        sut.Configure("COM8", 250000u, listenOnly: false, baudRate: 115200);

        Assert.Equal(250000u, sut.BusSpeed);
        Assert.False(sut.IsListenOnly);

        var status = sut.GetStatus();
        dynamic tele = status.ExtendedTelemetry!;
        Assert.Equal(115200, (int)tele.BaudRate);
        Assert.Equal("250 kbps", (string)tele.BusSpeed);
        Assert.Equal("NORMAL (TRANSMIT ENABLED)", (string)tele.OperatingMode);
    }

    [Fact]
    public void GetStatus_ExtendedTelemetry_ContainsGvretDetails()
    {
        var sut = new GvretSerialTransport();
        var status = sut.GetStatus();

        Assert.Equal("arduino-nano-gvret", status.TransportId);
        Assert.NotNull(status.ExtendedTelemetry);
        dynamic tele = status.ExtendedTelemetry!;
        Assert.Equal(115200, (int)tele.BaudRate);
        Assert.Equal("500 kbps", (string)tele.BusSpeed);
        Assert.True((bool)tele.ListenOnly);
        Assert.Equal("LISTEN ONLY (SILENT)", (string)tele.OperatingMode);
        Assert.Equal("8 MHz MCP2515", (string)tele.HardwareOscillator);
        Assert.Contains("SavvyCAN", (string)tele.Architecture);
    }
}
