using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EcuSimulatorAndSessionTests
{
    [Fact]
    public void EcuSimulator_ReadVinDid_ReturnsValidVinPayload()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);

        // Request: 0x22 0xF1 0x90 to ECM (0x7E0)
        byte[] req = new byte[] { 0x22, 0xF1, 0x90 };
        byte[]? resp = sim.ProcessDiagnosticRequest(0x7E0, req);

        Assert.NotNull(resp);
        Assert.Equal(0x62, resp[0]); // Positive response for 0x22
        Assert.Equal(0xF1, resp[1]);
        Assert.Equal(0x90, resp[2]);

        string vin = Encoding.ASCII.GetString(resp[3..]);
        Assert.Equal("1FA6P8CF5H5123456", vin);
    }

    [Fact]
    public void EcuSimulator_InvalidDid_ReturnsNegativeResponse0x31()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);

        // Request: 0x22 0x99 0x99 (Non-existent DID)
        byte[] req = new byte[] { 0x22, 0x99, 0x99 };
        byte[]? resp = sim.ProcessDiagnosticRequest(0x7E0, req);

        Assert.NotNull(resp);
        Assert.Equal(0x7F, resp[0]); // Negative Response
        Assert.Equal(0x22, resp[1]); // Service 0x22
        Assert.Equal(0x31, resp[2]); // NRC 0x31 RequestOutOfRange
    }

    [Fact]
    public async Task DiagnosticSessionService_SendRequest_CorrelatesResponse()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);
        using var diagService = new DiagnosticSessionService(pipeline, isoTp, sim);

        // Send UDS Read DID 0xF190 (VIN) to ECM (0x7E0) in simulation
        byte[] req = new byte[] { 0x22, 0xF1, 0x90 };
        var exchange = await diagService.SendRequestAsync(0x7E0, req, "ECM", isSimulation: true);

        // Give pipeline event loop time to process async simulation frames
        for (int i = 0; i < 15 && diagService.GetEcuInfo("ECM")?.Vin == null; i++)
        {
            await Task.Delay(50);
        }

        var exchanges = diagService.GetRecentExchanges();
        Assert.NotEmpty(exchanges);

        var completed = exchanges.FirstOrDefault(e => e.Id == exchange.Id);
        Assert.NotNull(completed);
        Assert.True(completed.IsComplete);
        Assert.True(completed.IsSuccess);
        Assert.Equal("UDS", completed.Protocol);

        // Verify ECU info was automatically populated
        var ecmInfo = diagService.GetEcuInfo("ECM");
        Assert.NotNull(ecmInfo);
        Assert.Equal("1FA6P8CF5H5123456", ecmInfo.Vin);
    }
}
