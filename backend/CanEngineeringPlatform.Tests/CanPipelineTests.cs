using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class CanPipelineTests
{
    [Fact]
    public async Task PublishFrame_ValidFrame_DispatchesToSubscribers()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        var receivedFrames = new List<CanFrame>();
        var tcs = new TaskCompletionSource<bool>();

        pipeline.OnFrameReceived += frame =>
        {
            lock (receivedFrames)
            {
                receivedFrames.Add(frame);
                if (receivedFrames.Count >= 5)
                {
                    tcs.TrySetResult(true);
                }
            }
        };

        for (int i = 0; i < 5; i++)
        {
            var frame = new CanFrame((uint)(i * 1000), (uint)(0x100 + i), 8, 0, new byte[] { (byte)i, 0, 0, 0, 0, 0, 0, 0 });
            await pipeline.PublishFrameAsync(frame);
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Equal(tcs.Task, completed);
        Assert.Equal(5, receivedFrames.Count);
        Assert.Equal(0x100U, receivedFrames[0].Id);
        Assert.Equal(0x104U, receivedFrames[4].Id);
    }
}
