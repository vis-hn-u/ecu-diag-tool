using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class IsoTpTransportTests
{
    [Fact]
    public void SingleFrame_ValidPayload_ReassemblesInstantly()
    {
        var session = new IsoTpSession(0x7E8, 0x7E0);

        // Single Frame with 3 bytes (0x22 0xF1 0x90): Data = [0x03, 0x22, 0xF1, 0x90, 0, 0, 0, 0]
        byte[] canData = new byte[8] { 0x03, 0x22, 0xF1, 0x90, 0, 0, 0, 0 };
        var frame = new CanFrame(1000, 0x7E8, 8, 0, canData);

        bool completed = session.ProcessIncomingCanFrame(frame, out var message);

        Assert.True(completed);
        Assert.NotNull(message);
        Assert.Equal(3, message.Length);
        Assert.Equal(0x22, message.Payload[0]);
        Assert.Equal(0xF1, message.Payload[1]);
        Assert.Equal(0x90, message.Payload[2]);
    }

    [Fact]
    public void MultiFrame_FirstAndConsecutiveFrames_ReassemblesSuccessfully()
    {
        var session = new IsoTpSession(0x7E8, 0x7E0);
        bool fcRequired = false;
        session.OnFlowControlRequired += fc => fcRequired = true;

        // Total 20 bytes payload
        // First Frame: Total length = 20 (0x014), PCI = 0x10 0x14, First 6 bytes [1..6]
        byte[] ffData = new byte[8] { 0x10, 0x14, 1, 2, 3, 4, 5, 6 };
        var ff = new CanFrame(1000, 0x7E8, 8, 0, ffData);

        bool ffCompleted = session.ProcessIncomingCanFrame(ff, out var msg1);
        Assert.False(ffCompleted);
        Assert.Null(msg1);
        Assert.True(fcRequired);
        Assert.Equal(IsoTpState.ReceivingConsecutiveFrames, session.State);

        // Consecutive Frame 1 (SN 1): Next 7 bytes [7..13]
        byte[] cf1Data = new byte[8] { 0x21, 7, 8, 9, 10, 11, 12, 13 };
        var cf1 = new CanFrame(2000, 0x7E8, 8, 0, cf1Data);
        bool cf1Completed = session.ProcessIncomingCanFrame(cf1, out var msg2);
        Assert.False(cf1Completed);
        Assert.Null(msg2);

        // Consecutive Frame 2 (SN 2): Remaining 7 bytes [14..20]
        byte[] cf2Data = new byte[8] { 0x22, 14, 15, 16, 17, 18, 19, 20 };
        var cf2 = new CanFrame(3000, 0x7E8, 8, 0, cf2Data);
        bool cf2Completed = session.ProcessIncomingCanFrame(cf2, out var finalMsg);

        Assert.True(cf2Completed);
        Assert.NotNull(finalMsg);
        Assert.Equal(20, finalMsg.Length);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal((byte)(i + 1), finalMsg.Payload[i]);
        }
        Assert.Equal(IsoTpState.Idle, session.State);
    }

    [Fact]
    public void ConsecutiveFrame_OutOfSequence_TriggersErrorAndResets()
    {
        var session = new IsoTpSession(0x7E8, 0x7E0);
        string? error = null;
        session.OnTransportError += err => error = err;

        // First Frame (Len 15)
        byte[] ffData = new byte[8] { 0x10, 0x0F, 1, 2, 3, 4, 5, 6 };
        session.ProcessIncomingCanFrame(new CanFrame(1000, 0x7E8, 8, 0, ffData), out _);

        // Wrong SN (Expected 1, got 3)
        byte[] cfWrongData = new byte[8] { 0x23, 7, 8, 9, 10, 11, 12, 13 };
        bool completed = session.ProcessIncomingCanFrame(new CanFrame(2000, 0x7E8, 8, 0, cfWrongData), out var msg);

        Assert.False(completed);
        Assert.Null(msg);
        Assert.NotNull(error);
        Assert.Contains("mismatch", error);
        Assert.Equal(IsoTpState.Idle, session.State);
    }

    [Fact]
    public void SegmentMessage_ShortPayload_GeneratesSingleFrame()
    {
        var session = new IsoTpSession(0x7E8, 0x7E0);
        byte[] payload = new byte[] { 0x22, 0xF1, 0x90 };

        var frames = session.SegmentMessage(payload, 0x7E0, 1000);

        Assert.Single(frames);
        Assert.Equal(0x7E0U, frames[0].Id);
        Assert.Equal(0x03, frames[0].Data[0]); // SF len 3
        Assert.Equal(0x22, frames[0].Data[1]);
        Assert.Equal(0xF1, frames[0].Data[2]);
        Assert.Equal(0x90, frames[0].Data[3]);
    }

    [Fact]
    public void SegmentMessage_LongPayload_GeneratesFirstAndConsecutiveFrames()
    {
        var session = new IsoTpSession(0x7E8, 0x7E0);
        byte[] payload = new byte[18];
        for (int i = 0; i < 18; i++) payload[i] = (byte)(i + 1);

        var frames = session.SegmentMessage(payload, 0x7E0, 1000);

        // Total 18 bytes: FF has 6 bytes, CF1 has 7 bytes, CF2 has 5 bytes -> Total 3 frames
        Assert.Equal(3, frames.Count);
        Assert.Equal(0x10, frames[0].Data[0]); // FF
        Assert.Equal(18, frames[0].Data[1]);
        Assert.Equal(0x21, frames[1].Data[0]); // CF SN 1
        Assert.Equal(0x22, frames[2].Data[0]); // CF SN 2
    }
}
