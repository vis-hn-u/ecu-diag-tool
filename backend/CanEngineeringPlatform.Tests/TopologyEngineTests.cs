using System.Linq;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class TopologyEngineTests
{
    private const string SampleDbc = @"VERSION ""1.0""
BU_: ECM BCM ABS CLUSTER
BO_ 280 EngineData: 8 ECM
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" CLUSTER,BCM
";

    [Fact]
    public void LoadDbc_PrepopulatesNodesAndEdges()
    {
        var engine = new TopologyEngine();
        var db = DbcParser.Parse(SampleDbc);
        engine.LoadDbc(db);

        var snapshot = engine.GetSnapshot();

        Assert.Equal(4, snapshot.Nodes.Count);
        Assert.Contains(snapshot.Nodes, n => n.Name == "ECM");
        Assert.Contains(snapshot.Nodes, n => n.Name == "CLUSTER");
        Assert.Contains(snapshot.Nodes, n => n.Name == "BCM");
        Assert.Contains(snapshot.Nodes, n => n.Name == "ABS");

        Assert.Single(snapshot.Messages);
        Assert.Equal(2, snapshot.Edges.Count); // ECM->CLUSTER, ECM->BCM
    }

    [Fact]
    public void ProcessFrame_KnownMessage_UpdatesNodeAndMessageStats()
    {
        var engine = new TopologyEngine();
        var db = DbcParser.Parse(SampleDbc);
        engine.LoadDbc(db);

        var msg280 = db.Messages[0];
        var frame = new CanFrame(1000, 280, 8, 0, new byte[] { 0x80, 0x3E, 0, 0, 0, 0, 0, 0 });
        var signals = SignalDecoder.DecodeFrame(frame, msg280);

        engine.ProcessFrame(frame, msg280, signals);

        var snapshot = engine.GetSnapshot();
        Assert.Equal(1, snapshot.TotalFramesObserved);

        var ecmNode = snapshot.Nodes.Find(n => n.Name == "ECM");
        Assert.NotNull(ecmNode);
        Assert.Equal(1, ecmNode.MessageCount);
        Assert.Equal("Active", ecmNode.Status);
    }

    [Fact]
    public void ProcessFrame_UnknownCanId_CreatesUnknownNodeAndMessage()
    {
        var engine = new TopologyEngine();

        var frame = new CanFrame(1000, 0x555, 4, 0, new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 });
        engine.ProcessFrame(frame, null, null);

        var snapshot = engine.GetSnapshot();
        Assert.Equal(1, snapshot.TotalFramesObserved);

        var unknownMsg = snapshot.Messages.Find(m => m.Id == 0x555);
        Assert.NotNull(unknownMsg);
        Assert.False(unknownMsg.IsKnown);
        Assert.Equal("UNKNOWN (0x555)", unknownMsg.Name);
    }
}
