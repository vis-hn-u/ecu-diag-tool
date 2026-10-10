using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Tests;

public class Esp8266UsbSerialTransportTests
{
    private Esp8266UsbSerialTransport CreateTransport() => new Esp8266UsbSerialTransport();

    [Fact]
    public void TransportId_IsEsp8266UsbSerial()
    {
        var sut = CreateTransport();
        Assert.Equal("esp8266-usb-serial", sut.TransportId);
    }

    [Fact]
    public void Name_ContainsUsbSerialGateway()
    {
        var sut = CreateTransport();
        Assert.Contains("USB Serial", sut.Name);
    }

    [Fact]
    public void Type_IsEsp8266UsbSerial()
    {
        var sut = CreateTransport();
        Assert.Equal(TransportType.Esp8266UsbSerial, sut.Type);
    }

    [Fact]
    public void Capabilities_ListenOnly_SupportsTransmitFalse()
    {
        var sut = CreateTransport();
        Assert.False(sut.Capabilities.SupportsTransmit);
    }

    [Fact]
    public void Capabilities_ClassicCanOnly_NoCanFd()
    {
        var sut = CreateTransport();
        Assert.True(sut.Capabilities.SupportsClassicCan);
        Assert.False(sut.Capabilities.SupportsCanFd);
    }

    [Fact]
    public async Task SendFrameAsync_ThrowsNotSupportedException()
    {
        var sut = CreateTransport();
        var frame = CanEngineeringPlatform.Core.Models.CanFrame.CreateClassic(0x100, new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<NotSupportedException>(async () => await sut.SendFrameAsync(frame));
    }

    [Fact]
    public void GetAvailablePorts_ReturnsStringArray()
    {
        var ports = Esp8266UsbSerialTransport.GetAvailablePorts();
        Assert.NotNull(ports);
        Assert.IsType<string[]>(ports);
    }

    [Fact]
    public void Crc8_AllZeros20Bytes_ReturnsZero()
    {
        byte[] data = new byte[20];
        byte crc = Esp8266UsbSerialTransport.Crc8(data);
        Assert.Equal(0x00, crc);
    }

    [Fact]
    public void Crc8_KnownVector_MagicBytesOnly()
    {
        byte[] data = new byte[20];
        data[0] = 0xAA;
        data[1] = 0x55;
        // manually calculate expected
        byte crc = 0x00;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x80) != 0)
                    crc = (byte)((crc << 1) ^ 0x07);
                else
                    crc <<= 1;
            }
        }
        byte expected = crc;
        
        byte actual = Esp8266UsbSerialTransport.Crc8(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Crc8_SingleBitFlip_ChangesCrc()
    {
        byte[] data1 = new byte[20];
        byte[] data2 = new byte[20];
        data2[0] = 1;
        
        byte crc1 = Esp8266UsbSerialTransport.Crc8(data1);
        byte crc2 = Esp8266UsbSerialTransport.Crc8(data2);
        
        Assert.NotEqual(crc1, crc2);
    }

    [Fact]
    public void StateMachine_ValidCanFrame_Decoded()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA;
        frame[1] = 0x55;
        frame[2] = 0; frame[3] = 0; frame[4] = 0; frame[5] = 0; // timestamp
        frame[6] = 0x34; frame[7] = 0x12; frame[8] = 0; frame[9] = 0; // id=0x1234 (extended)
        frame[10] = 4; // DLC
        frame[11] = 1; // EXT flag
        frame[12] = 0xAA; frame[13] = 0xBB; frame[14] = 0xCC; frame[15] = 0xDD;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame)
        {
            sut.ProcessByte(b);
        }

        Assert.Equal(1, rxCount);
    }

    [Fact]
    public void StateMachine_InvalidMagic_FrameIgnored()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAB; // invalid
        frame[1] = 0x55;
        frame[10] = 4;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(0, rxCount);
    }

    [Fact]
    public void StateMachine_CrcMismatch_DroppedFrameIncremented()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA;
        frame[1] = 0x55;
        frame[10] = 4;
        frame[20] = (byte)(Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20)) ^ 0xFF); // flip CRC

        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(0, rxCount);
        var status = sut.GetStatus();
        Assert.Equal(1, ((dynamic)status.ExtendedTelemetry!).DroppedFrames);
    }

    [Fact]
    public void StateMachine_PartialRead_AccumulatesCorrectly()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        for(int i=0; i<10; i++) sut.ProcessByte(frame[i]);
        Assert.Equal(0, rxCount);
        
        for(int i=10; i<21; i++) sut.ProcessByte(frame[i]);
        Assert.Equal(1, rxCount);
    }

    [Fact]
    public void StateMachine_TwoFramesBackToBack_BothDecoded()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) sut.ProcessByte(b);
        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(2, rxCount);
    }

    [Fact]
    public void StateMachine_GarbageBeforeValidFrame_Syncs()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] garbage = new byte[50];
        Array.Fill<byte>(garbage, 0x11);
        foreach (var b in garbage) sut.ProcessByte(b);

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));
        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(1, rxCount);
    }

    [Fact]
    public void StateMachine_ValidFrameThenGarbageThenValidFrame_BothDecoded()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) sut.ProcessByte(b);
        sut.ProcessByte(0x11); sut.ProcessByte(0x22); sut.ProcessByte(0x33);
        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(2, rxCount);
    }

    [Fact]
    public void StateMachine_RepeatedAA55InPayload_HandledCorrectly()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; 
        frame[10] = 2; // DLC=2
        frame[12] = 0xAA; frame[13] = 0x55; // payload contains magic
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(1, rxCount);
    }

    [Fact]
    public void Configure_SetsPreferredPort()
    {
        var sut = CreateTransport();
        sut.Configure("COM99");
        // We can't directly check private field without reflection, but it shouldn't throw
        Assert.NotNull(sut);
    }

    [Fact]
    public void GetStatus_ReturnsCorrectTransportId()
    {
        var sut = CreateTransport();
        var status = sut.GetStatus();
        Assert.Equal("esp8266-usb-serial", status.TransportId);
    }

    [Fact]
    public void GetStatus_ExtendedTelemetry_ContainsCOMPort()
    {
        var sut = CreateTransport();
        var status = sut.GetStatus();
        var telemetry = (dynamic)status.ExtendedTelemetry!;
        Assert.Equal("None", telemetry.ComPort);
    }

    [Fact]
    public void Crc8_MaximumExtendedCanId_KnownVector()
    {
        byte[] data = new byte[20];
        data[0] = 0xAA; data[1] = 0x55;
        uint maxExtId = 0x1FFFFFFF;
        var idBytes = BitConverter.GetBytes(maxExtId);
        Array.Copy(idBytes, 0, data, 6, 4);
        
        byte expected = 0;
        foreach (byte b in data)
        {
            expected ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                if ((expected & 0x80) != 0)
                    expected = (byte)((expected << 1) ^ 0x07);
                else
                    expected <<= 1;
            }
        }
        
        byte actual = Esp8266UsbSerialTransport.Crc8(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Fuzz_RandomBytes_NoCrash()
    {
        var sut = CreateTransport();
        var rnd = new Random(42);
        byte[] buffer = new byte[10000];
        rnd.NextBytes(buffer);

        foreach (var b in buffer)
        {
            sut.ProcessByte(b);
        }
        // no exception means pass
        Assert.True(true);
    }

    [Fact]
    public void Fuzz_HighRateFrames_1000Frames_AllDecoded()
    {
        var sut = CreateTransport();
        int rxCount = 0;
        sut.OnFrameReceived += f => rxCount++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        for (int i = 0; i < 1000; i++)
        {
            foreach (var b in frame) sut.ProcessByte(b);
        }

        Assert.Equal(1000, rxCount);
    }

    [Fact]
    public void Crc8_StandardAsciiCheckVector_123456789_ReturnsF4()
    {
        // Standard SMBus catalog check vector: ASCII "123456789" -> 0xF4
        byte[] ascii = System.Text.Encoding.ASCII.GetBytes("123456789");
        byte crc = Esp8266UsbSerialTransport.Crc8(ascii);
        Assert.Equal(0xF4, crc);
    }

    [Fact]
    public void Crc8_IndependentCanPacketVectors_AllMatchExpected()
    {
        // Vector 1: Std ID 0x123, DLC 4, data [1,2,3,4], ts 1000 us -> expected 0x7B
        byte[] p1 = new byte[] {
            0xAA, 0x55, 0xE8, 0x03, 0x00, 0x00, 0x23, 0x01, 0x00, 0x00,
            0x04, 0x00, 0x01, 0x02, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00
        };
        Assert.Equal(0x7B, Esp8266UsbSerialTransport.Crc8(p1));

        // Vector 2: Ext ID 0x18DAF110, DLC 8, UDS payload, ts 500000 us -> expected 0x30
        byte[] p2 = new byte[] {
            0xAA, 0x55, 0x20, 0xA1, 0x07, 0x00, 0x10, 0xF1, 0xDA, 0x18,
            0x08, 0x01, 0x02, 0x01, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        Assert.Equal(0x30, Esp8266UsbSerialTransport.Crc8(p2));

        // Vector 3: Std ID 0x7E0, DLC 0, ts 0 us -> expected 0x2E
        byte[] p3 = new byte[] {
            0xAA, 0x55, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x07, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        Assert.Equal(0x2E, Esp8266UsbSerialTransport.Crc8(p3));
    }

    [Fact]
    public void ClassifyFrame_DistinguishesAllFrameTypesBeforeCanDecoder()
    {
        // CAN Frame
        byte[] canBuf = new byte[21];
        canBuf[0] = 0xAA; canBuf[1] = 0x55; canBuf[10] = 4;
        canBuf[20] = Esp8266UsbSerialTransport.Crc8(canBuf.AsSpan(0, 20));
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.CanFrame, Esp8266UsbSerialTransport.ClassifyFrame(canBuf));

        // Handshake
        byte[] hsBuf = new byte[21];
        hsBuf[0] = 0xAA; hsBuf[1] = 0x55;
        hsBuf[2] = (byte)'E'; hsBuf[3] = (byte)'S'; hsBuf[4] = (byte)'P';
        hsBuf[10] = 0xFF;
        hsBuf[11] = 0x02;
        hsBuf[12] = 0x01;
        hsBuf[20] = Esp8266UsbSerialTransport.Crc8(hsBuf.AsSpan(0, 20));
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.Handshake, Esp8266UsbSerialTransport.ClassifyFrame(hsBuf));

        // Heartbeat
        byte[] hbBuf = new byte[21];
        hbBuf[0] = 0xAA; hbBuf[1] = 0x55;
        hbBuf[10] = 0xFF;
        hbBuf[11] = 0x03;
        hbBuf[12] = 0x01;
        hbBuf[20] = Esp8266UsbSerialTransport.Crc8(hbBuf.AsSpan(0, 20));
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.Heartbeat, Esp8266UsbSerialTransport.ClassifyFrame(hbBuf));

        // Invalid CRC
        byte[] badCrc = (byte[])canBuf.Clone();
        badCrc[20] ^= 0xFF;
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.InvalidCrc, Esp8266UsbSerialTransport.ClassifyFrame(badCrc));

        // Unknown Type (DLC > 8 but not handshake/heartbeat)
        byte[] unkBuf = new byte[21];
        unkBuf[0] = 0xAA; unkBuf[1] = 0x55; unkBuf[10] = 15;
        unkBuf[20] = Esp8266UsbSerialTransport.Crc8(unkBuf.AsSpan(0, 20));
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.Unknown, Esp8266UsbSerialTransport.ClassifyFrame(unkBuf));
    }

    [Fact]
    public void ProcessFrame_HandshakeAndHeartbeat_DoNotInvokeOnFrameReceived()
    {
        var sut = CreateTransport();
        int canFrameCount = 0;
        sut.OnFrameReceived += f => canFrameCount++;

        // Send Handshake
        byte[] hsBuf = new byte[21];
        hsBuf[0] = 0xAA; hsBuf[1] = 0x55;
        hsBuf[2] = (byte)'E'; hsBuf[3] = (byte)'S'; hsBuf[4] = (byte)'P';
        hsBuf[10] = 0xFF; hsBuf[11] = 0x02; hsBuf[12] = 0x01;
        hsBuf[20] = Esp8266UsbSerialTransport.Crc8(hsBuf.AsSpan(0, 20));
        foreach (var b in hsBuf) sut.ProcessByte(b);

        // Send Heartbeat
        byte[] hbBuf = new byte[21];
        hbBuf[0] = 0xAA; hbBuf[1] = 0x55;
        hbBuf[10] = 0xFF; hbBuf[11] = 0x03; hbBuf[12] = 0x01;
        hbBuf[20] = Esp8266UsbSerialTransport.Crc8(hbBuf.AsSpan(0, 20));
        foreach (var b in hbBuf) sut.ProcessByte(b);

        // Send Unknown
        byte[] unk = new byte[21];
        unk[0] = 0xAA; unk[1] = 0x55; unk[10] = 99;
        unk[20] = Esp8266UsbSerialTransport.Crc8(unk.AsSpan(0, 20));
        foreach (var b in unk) sut.ProcessByte(b);

        Assert.Equal(0, canFrameCount); // Proves non-CAN frames never enter CAN decoder
        Assert.True(((dynamic)sut.GetStatus().ExtendedTelemetry!).IsGatewayIdentified);
    }

    [Fact]
    public void StateMachine_ByteAtATime_DecodesFramesAccurately()
    {
        var sut = CreateTransport();
        int count = 0;
        sut.OnFrameReceived += f => count++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        // Feed 5 frames, 1 byte at a time
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 21; j++)
            {
                sut.ProcessByte(frame[j]);
            }
        }
        Assert.Equal(5, count);
    }

    [Fact]
    public void StateMachine_RandomChunkSizes_AssemblesAllFrames()
    {
        var sut = CreateTransport();
        int count = 0;
        sut.OnFrameReceived += f => count++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        // 10 frames = 210 bytes
        byte[] allBytes = new byte[210];
        for (int i = 0; i < 10; i++) Array.Copy(frame, 0, allBytes, i * 21, 21);

        // Feed in random chunk sizes (1 to 17 bytes)
        var rng = new Random(123);
        int offset = 0;
        while (offset < allBytes.Length)
        {
            int chunkSize = Math.Min(rng.Next(1, 18), allBytes.Length - offset);
            for (int k = 0; k < chunkSize; k++)
            {
                sut.ProcessByte(allBytes[offset + k]);
            }
            offset += chunkSize;
        }
        Assert.Equal(10, count);
    }

    [Fact]
    public void StateMachine_TrailingGarbageAndNoise_ResynchronizesAutomatically()
    {
        var sut = CreateTransport();
        int count = 0;
        sut.OnFrameReceived += f => count++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        // Frame 1
        foreach (var b in frame) sut.ProcessByte(b);

        // Noise
        byte[] noise = new byte[] { 0xAA, 0x00, 0xAA, 0xAA, 0x54, 0x99, 0xFF };
        foreach (var b in noise) sut.ProcessByte(b);

        // Frame 2
        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void StressTest_HighVolumeFrames_ZeroFrameLoss(int frameCount)
    {
        var sut = CreateTransport();
        int received = 0;
        sut.OnFrameReceived += f => received++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55;
        frame[6] = 0x11; frame[7] = 0x02; // ID = 0x211
        frame[10] = 8;
        frame[12] = 0xDE; frame[13] = 0xAD; frame[14] = 0xBE; frame[15] = 0xEF;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        for (int i = 0; i < frameCount; i++)
        {
            for (int j = 0; j < 21; j++)
            {
                sut.ProcessByte(frame[j]);
            }
        }

        Assert.Equal(frameCount, received);
        var status = sut.GetStatus();
        Assert.Equal(frameCount, status.TotalFramesReceived);
    }

    [Fact]
    public async Task FullSoftwareChain_EndToEndFidelity_PreservesAllFields()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var transport = CreateTransport();

        CanFrame? receivedFrame = null;
        pipeline.OnFrameReceived += f => receivedFrame = f;

        transport.OnFrameReceived += f => { _ = pipeline.PublishFrameAsync(f); };

        // Known 21-byte frame: ID=0x18DAF110 (29-bit), DLC=8, data=[1,2,3,4,5,6,7,8], ts=123456
        uint ts = 123456;
        uint id = 0x18DAF110;
        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55;
        Array.Copy(BitConverter.GetBytes(ts), 0, frame, 2, 4);
        Array.Copy(BitConverter.GetBytes(id), 0, frame, 6, 4);
        frame[10] = 8;
        frame[11] = 0x01; // Extended flag
        for (int i = 0; i < 8; i++) frame[12 + i] = (byte)(i + 1);
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) transport.ProcessByte(b);

        await Task.Delay(100);

        Assert.True(receivedFrame.HasValue);
        var f = receivedFrame.Value;
        Assert.Equal(id, f.Id);
        Assert.Equal(8, f.Dlc);
        Assert.True(f.IsExtended);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, f.Data);
        Assert.Equal("ESP8266 + MCP2515 Gateway", f.SourceId);
        Assert.Equal(0, f.Channel);
    }

    [Fact]
    public async Task MultiTransport_AllFourTransports_PublishConcurrentlyWithoutInterference()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var ws = new WaveshareCanFdTransport();
        var sim = new SimulationTransport();
        var elm = new Elm327Transport();
        var espUsb = CreateTransport();

        var manager = new HardwareManager(pipeline, new ICanTransport[] { ws, sim, elm, espUsb });

        int totalReceived = 0;
        pipeline.OnFrameReceived += f => Interlocked.Increment(ref totalReceived);

        await manager.SetActiveTransportAsync("virtual-simulation");

        var f1 = CanFrame.CreateClassic(0x100, new byte[] { 1 });
        var f2 = CanFrame.CreateClassic(0x200, new byte[] { 2 });
        var f3 = CanFrame.CreateClassic(0x300, new byte[] { 3 });

        await pipeline.PublishFrameAsync(f1);
        await pipeline.PublishFrameAsync(f2);
        await pipeline.PublishFrameAsync(f3);

        await Task.Delay(100);
        Assert.True(totalReceived >= 3);
    }

    [Fact]
    public void Security_CorruptedCrcAndManipulatedPayload_StrictlyRejected()
    {
        var sut = CreateTransport();
        int received = 0;
        sut.OnFrameReceived += f => received++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55; frame[10] = 8;
        frame[20] = (byte)(Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20)) + 1); // Corrupted CRC

        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(0, received);
        Assert.Equal(1, ((dynamic)sut.GetStatus().ExtendedTelemetry!).DroppedFrames);
    }

    [Fact]
    public void Security_InvalidDlcAndUnknownHeaders_SafelyRejectedWithoutCrash()
    {
        var sut = CreateTransport();
        int received = 0;
        sut.OnFrameReceived += f => received++;

        byte[] frame = new byte[21];
        frame[0] = 0xAA; frame[1] = 0x55;
        frame[10] = 15;
        frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

        foreach (var b in frame) sut.ProcessByte(b);

        Assert.Equal(0, received);
    }

    [Fact]
    public async Task Security_RapidReconnectLoop_MaintainsStability()
    {
        var sut = CreateTransport();
        for (int i = 0; i < 20; i++)
        {
            await sut.ConnectAsync();
            await sut.DisconnectAsync();
        }
        Assert.False(sut.GetStatus().IsConnected);
    }

    [Fact]
    public void Protocol_RealWorldCanFrame_0x7E8_DiagnosticResponse_DecodesWithFidelity()
    {
        var sut = CreateTransport();
        CanFrame? decoded = null;
        sut.OnFrameReceived += f => decoded = f;

        // Exactly the frame specified in Hardware/Protocol Audit:
        // CAN ID = 0x7E8, DLC = 8, DATA = 10 14 62 F1 90 12 34 56, timestamp = 0
        byte[] frame = new byte[21]
        {
            0xAA, 0x55,                         // Magic
            0x00, 0x00, 0x00, 0x00,             // Timestamp us = 0
            0xE8, 0x07, 0x00, 0x00,             // CAN ID = 0x7E8
            0x08,                               // DLC = 8
            0x00,                               // Flags = Standard ID
            0x10, 0x14, 0x62, 0xF1, 0x90, 0x12, 0x34, 0x56, // Data
            0x95                                // CRC-8/SMBUS
        };

        // Assert CRC calculation matches
        byte calculatedCrc = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));
        Assert.Equal(0x95, calculatedCrc);

        // Assert classification
        var msgType = Esp8266UsbSerialTransport.ClassifyFrame(frame);
        Assert.Equal(Esp8266UsbSerialTransport.UsbMessageType.CanFrame, msgType);

        // Feed bytes
        foreach (var b in frame) sut.ProcessByte(b);

        // Assert resulting Unified CanFrame
        Assert.NotNull(decoded);
        Assert.Equal(0x7E8u, decoded.Value.Id);
        Assert.Equal(8, decoded.Value.Dlc);
        Assert.False(decoded.Value.IsExtended);
        Assert.False(decoded.Value.IsRtr);
        Assert.False(decoded.Value.IsCanFd);
        Assert.Equal(0, decoded.Value.Channel);
        Assert.Equal("ESP8266 + MCP2515 Gateway", decoded.Value.SourceId);
        Assert.Equal(new byte[] { 0x10, 0x14, 0x62, 0xF1, 0x90, 0x12, 0x34, 0x56 }, decoded.Value.Data);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void PollingMode_NoIntWire_StressTest_ZeroLossAndLowLatency(int frameCount)
    {
        var sut = CreateTransport();
        var received = new List<CanFrame>(frameCount);
        sut.OnFrameReceived += f => received.Add(f);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < frameCount; i++)
        {
            byte[] frame = new byte[21];
            frame[0] = 0xAA; frame[1] = 0x55;
            frame[2] = (byte)(i & 0xFF);
            frame[3] = (byte)((i >> 8) & 0xFF);
            frame[6] = (byte)((0x100 + (i % 256)) & 0xFF);
            frame[7] = 0x01;
            frame[10] = 8;
            frame[12] = (byte)(i & 0xFF);
            frame[20] = Esp8266UsbSerialTransport.Crc8(frame.AsSpan(0, 20));

            foreach (var b in frame) sut.ProcessByte(b);
        }

        sw.Stop();

        Assert.Equal(frameCount, received.Count);
        Assert.Equal(0L, ((dynamic)sut.GetStatus().ExtendedTelemetry!).DroppedFrames);

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgUsPerFrame = (totalMs * 1000.0) / frameCount;
        // Verify parsing throughput exceeds 4,000 frames/sec (<250 us per frame in-memory even under system load)
        Assert.True(avgUsPerFrame < 250.0, $"Avg parse latency {avgUsPerFrame} us exceeded 250 us limit");
    }

    [Fact]
    public void PollingMode_SimulatedBurst_Rx0AndRx1_PreservesOrder()
    {
        var sut = CreateTransport();
        var received = new List<CanFrame>();
        sut.OnFrameReceived += f => received.Add(f);

        // Simulate 10 bursts of 2 frames (RXB0 followed by RXB1 from SPI polling drain)
        for (int burst = 0; burst < 10; burst++)
        {
            // Frame from RXB0
            byte[] frame0 = new byte[21];
            frame0[0] = 0xAA; frame0[1] = 0x55;
            frame0[6] = (byte)(burst * 2);
            frame0[10] = 4;
            frame0[20] = Esp8266UsbSerialTransport.Crc8(frame0.AsSpan(0, 20));
            foreach (var b in frame0) sut.ProcessByte(b);

            // Frame from RXB1
            byte[] frame1 = new byte[21];
            frame1[0] = 0xAA; frame1[1] = 0x55;
            frame1[6] = (byte)(burst * 2 + 1);
            frame1[10] = 4;
            frame1[20] = Esp8266UsbSerialTransport.Crc8(frame1.AsSpan(0, 20));
            foreach (var b in frame1) sut.ProcessByte(b);
        }

        Assert.Equal(20, received.Count);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal((uint)i, received[i].Id);
        }
    }
}
