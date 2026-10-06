using System;
using System.Linq;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class DbcParserAndSignalDecoderTests
{
    private const string SampleDbc = @"VERSION ""1.0""

BU_: ECM ABS BCM

BO_ 280 EngineData: 8 ECM
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" BCM
 SG_ EngineTorque : 16|8@1+ (1,-100) [-100|155] ""Nm"" ABS
 SG_ SteeringAngle : 24|16@1- (0.1,0) [-720|720] ""deg"" BCM

BO_ 390 VehicleDynamics: 8 ABS
 SG_ VehicleSpeed : 7|16@0+ (0.01,0) [0|250] ""km/h"" ECM
";

    [Fact]
    public void DbcParser_ParsesNodesMessagesSignals_Successfully()
    {
        var db = DbcParser.Parse(SampleDbc);

        Assert.Equal(3, db.Nodes.Count);
        Assert.Contains(db.Nodes, n => n.Name == "ECM");
        Assert.Contains(db.Nodes, n => n.Name == "ABS");
        Assert.Contains(db.Nodes, n => n.Name == "BCM");

        Assert.Equal(2, db.Messages.Count);
        var msg280 = db.Messages.Find(m => m.Id == 280);
        Assert.NotNull(msg280);
        Assert.Equal("EngineData", msg280.Name);
        Assert.Equal(8, msg280.Dlc);
        Assert.Equal("ECM", msg280.Transmitter);
        Assert.Equal(3, msg280.Signals.Count);

        var speedSig = msg280.Signals.Find(s => s.Name == "EngineSpeed");
        Assert.NotNull(speedSig);
        Assert.Equal(0, speedSig.StartBit);
        Assert.Equal(16, speedSig.Length);
        Assert.True(speedSig.IsLittleEndian);
        Assert.False(speedSig.IsSigned);
        Assert.Equal(0.125, speedSig.Factor);
        Assert.Equal(0.0, speedSig.Offset);
        Assert.Equal("rpm", speedSig.Unit);
        Assert.Contains("BCM", speedSig.Receivers);
    }

    [Fact]
    public void SignalDecoder_LittleEndian_DecodesCorrectly()
    {
        var db = DbcParser.Parse(SampleDbc);
        var msg280 = db.Messages.Find(m => m.Id == 280)!;

        // EngineSpeed = 2000 rpm -> Raw = 2000 / 0.125 = 16000 = 0x3E80
        // Data[0] = 0x80, Data[1] = 0x3E
        // EngineTorque = 50 Nm -> Raw = 50 - (-100) = 150 = 0x96 -> Data[2] = 0x96
        // SteeringAngle = -45.0 deg -> Raw = -450 -> 16-bit 2's complement = 0xFE3E -> Data[3] = 0x3E, Data[4] = 0xFE
        byte[] payload = new byte[8] { 0x80, 0x3E, 0x96, 0x3E, 0xFE, 0x00, 0x00, 0x00 };
        var frame = new CanFrame(1000, 280, 8, 0, payload);

        var decoded = SignalDecoder.DecodeFrame(frame, msg280);

        Assert.Equal(3, decoded.Count);

        var rpm = decoded.Find(s => s.SignalName == "EngineSpeed");
        Assert.NotNull(rpm);
        Assert.Equal(16000, rpm.RawValue);
        Assert.Equal(2000.0, rpm.EngineeringValue);
        Assert.Equal("rpm", rpm.Unit);

        var torque = decoded.Find(s => s.SignalName == "EngineTorque");
        Assert.NotNull(torque);
        Assert.Equal(150, torque.RawValue);
        Assert.Equal(50.0, torque.EngineeringValue);
        Assert.Equal("Nm", torque.Unit);

        var steer = decoded.Find(s => s.SignalName == "SteeringAngle");
        Assert.NotNull(steer);
        Assert.Equal(-45.0, steer.EngineeringValue);
        Assert.Equal("deg", steer.Unit);
    }

    [Fact]
    public void SignalDecoder_MotorolaBigEndian_DecodesCorrectly()
    {
        var db = DbcParser.Parse(SampleDbc);
        var msg390 = db.Messages.Find(m => m.Id == 390)!;

        // VehicleSpeed: StartBit 7, Length 16 @0+ (0.01, 0)
        // VehicleSpeed = 100.0 km/h -> Raw = 10000 = 0x2710
        // Motorola StartBit 7 MSB -> Byte 0 = 0x27, Byte 1 = 0x10
        byte[] payload = new byte[8] { 0x27, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var frame = new CanFrame(2000, 390, 8, 0, payload);

        var decoded = SignalDecoder.DecodeFrame(frame, msg390);

        Assert.Single(decoded);
        var spd = decoded[0];
        Assert.Equal("VehicleSpeed", spd.SignalName);
        Assert.Equal(10000, spd.RawValue);
        Assert.Equal(100.0, spd.EngineeringValue);
        Assert.Equal("km/h", spd.Unit);
    }
}
