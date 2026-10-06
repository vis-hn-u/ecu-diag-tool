using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Engine;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EngineToCanPipelineIntegrationTests
{
    [Fact]
    public async Task EngineCanBridge_PublishingToPipeline_CapturesEngineDataFrames()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var engSim = new EngineSimulator();

        using var bridge = new EngineCanBridgeService(engSim, pipeline, ecuSim);

        var receivedFrames = new List<CanFrame>();
        var tcs = new TaskCompletionSource<bool>();

        pipeline.OnFrameReceived += frame =>
        {
            lock (receivedFrames)
            {
                receivedFrames.Add(frame);
                if (receivedFrames.Count >= 2)
                {
                    tcs.TrySetResult(true);
                }
            }
        };

        bridge.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        bridge.Stop();

        Assert.Equal(tcs.Task, completed);
        Assert.NotEmpty(receivedFrames);

        bool hasFrame280 = receivedFrames.Exists(f => f.Id == 0x280);
        Assert.True(hasFrame280);
    }
}
