using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Engine;

public interface IEngineCanBridgeService
{
    bool IsRunning { get; }
    void Start();
    void Stop();
    void SetThrottle(double tps);
    void SetGear(int gear);
    EngineOutputs GetCurrentOutputs();
}

public class EngineCanBridgeService : IEngineCanBridgeService, IDisposable
{
    private readonly IEngineSimulator _engineSimulator;
    private readonly ICanPipeline _pipeline;
    private readonly IEcuSimulator _ecuSimulator;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    public bool IsRunning { get; private set; } = false;

    public EngineCanBridgeService(
        IEngineSimulator engineSimulator,
        ICanPipeline pipeline,
        IEcuSimulator ecuSimulator)
    {
        _engineSimulator = engineSimulator;
        _pipeline = pipeline;
        _ecuSimulator = ecuSimulator;
    }

    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => SimulationLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void SetThrottle(double tps)
    {
        _engineSimulator.Inputs.ThrottlePositionPct = Math.Clamp(tps, 0.0, 100.0);
    }

    public void SetGear(int gear)
    {
        _engineSimulator.Inputs.Gear = Math.Clamp(gear, 1, 6);
    }

    public EngineOutputs GetCurrentOutputs() => _engineSimulator.CurrentOutputs;

    private async Task SimulationLoopAsync(CancellationToken token)
    {
        uint timestampUs = 0;
        while (!token.IsCancellationRequested)
        {
            var outputs = _engineSimulator.Step(0.05); // 50ms step = 20Hz
            timestampUs += 50000;

            // 1. Frame 0x280 (EngineData)
            // Bytes 0-1: RPM * 8 (factor 0.125)
            // Byte 2: Torque + 100 (offset -100)
            // Byte 3: Throttle Position %
            // Byte 4: Coolant Temp + 40
            ushort rawRpm = (ushort)Math.Clamp(outputs.Rpm * 8.0, 0, 65535);
            byte rawTorque = (byte)Math.Clamp(outputs.TorqueNm + 100.0, 0, 255);
            byte rawTps = (byte)Math.Clamp(_engineSimulator.Inputs.ThrottlePositionPct * 2.55, 0, 255);
            byte rawCoolant = (byte)Math.Clamp(outputs.CoolantTempC + 40.0, 0, 255);

            byte[] frame280 = new byte[8]
            {
                (byte)(rawRpm & 0xFF),
                (byte)((rawRpm >> 8) & 0xFF),
                rawTorque,
                rawTps,
                rawCoolant,
                (byte)Math.Clamp(outputs.EngineLoadPct, 0, 255),
                (byte)Math.Clamp(outputs.BoostBar * 100.0, 0, 255),
                (byte)Math.Clamp(outputs.Afr * 10.0, 0, 255)
            };
            await _pipeline.PublishFrameAsync(new CanFrame(timestampUs, 0x280, 8, 0, frame280));

            // 2. Frame 0x390 (VehicleDynamics)
            // Bytes 0-1: Speed * 100 (factor 0.01)
            // Byte 2: Gear
            ushort rawSpeed = (ushort)Math.Clamp(outputs.VehicleSpeedKmh * 100.0, 0, 65535);
            byte[] frame390 = new byte[8]
            {
                (byte)(rawSpeed & 0xFF),
                (byte)((rawSpeed >> 8) & 0xFF),
                (byte)_engineSimulator.Inputs.Gear,
                0, 0, 0, 0, 0
            };
            await _pipeline.PublishFrameAsync(new CanFrame(timestampUs, 0x390, 8, 0, frame390));

            // 3. Update Virtual ECM DIDs in ECU Simulator
            var ecm = _ecuSimulator.GetEcuByRequestCanId(0x7E0);
            if (ecm != null)
            {
                // DID 0x0100: RPM
                ecm.DataIdentifiers[0x0100] = new byte[] { (byte)((int)outputs.Rpm >> 8), (byte)((int)outputs.Rpm & 0xFF) };
                // DID 0x0101: Coolant Temp
                ecm.DataIdentifiers[0x0101] = new byte[] { (byte)outputs.CoolantTempC };
                // DID 0x0102: Battery Voltage (13.8V -> 13800 mV)
                ecm.DataIdentifiers[0x0102] = new byte[] { 0x35, 0xE8 };
            }

            try
            {
                await Task.Delay(50, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
