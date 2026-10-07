using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Core.Pipeline;

public class CanFrameBatch
{
    public List<CanFrame> Frames { get; set; } = new();
    public List<DecodedSignal> Signals { get; set; } = new();
    public double FrameRate { get; set; }
}

public interface ICanPipeline
{
    ValueTask PublishFrameAsync(CanFrame frame);
    void SetActiveDbc(DbcDatabase? dbc);
    void ResetDefaultDbc();
    DbcDatabase? GetActiveDbc();
    event Action<CanFrame>? OnFrameReceived;
    event Action<IReadOnlyList<DecodedSignal>>? OnSignalsDecoded;
    event Action<CanFrameBatch>? OnBatchProcessed;
    long TotalFramesProcessed { get; }
    double CurrentFps { get; }
}

public class CanPipeline : ICanPipeline, IDisposable
{
    private readonly ITopologyEngine _topologyEngine;
    private readonly ICanLoggerService _loggerService;
    private readonly Channel<CanFrame> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;

    private DbcDatabase? _activeDbc;
    private readonly object _dbcLock = new();

    private long _totalFrames;
    // NOTE: _windowFrames is accessed only on the single ProcessQueueAsync thread
    // (guaranteed by BoundedChannelOptions.SingleReader = true). No Interlocked needed.
    private int _windowFrames;
    private DateTime _lastFpsCalc = DateTime.UtcNow;
    // _currentFps is written on the pipeline thread and read from any thread.
    // C# does not allow volatile on double; use a long-bits backing field + Interlocked for safety.
    private long _currentFpsBits;
    private double _currentFps
    {
        get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _currentFpsBits));
        set => Interlocked.Exchange(ref _currentFpsBits, BitConverter.DoubleToInt64Bits(value));
    }

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<IReadOnlyList<DecodedSignal>>? OnSignalsDecoded;
    public event Action<CanFrameBatch>? OnBatchProcessed;

    public long TotalFramesProcessed => Interlocked.Read(ref _totalFrames);
    public double CurrentFps => _currentFps;

    public CanPipeline(ITopologyEngine topologyEngine, ICanLoggerService loggerService)
    {
        _topologyEngine = topologyEngine;
        _loggerService = loggerService;

        // Bounded channel to prevent runaway memory if consumers lag
        _channel = Channel.CreateBounded<CanFrame>(new BoundedChannelOptions(50000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

        // Preload authoritative Magneti Marelli MJD (Swift DDiS) DBC by default
        // Ensures live signals decode and print immediately upon hardware connection
        SetActiveDbc(GetDefaultMarelliSwiftDbc());

        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    public ValueTask PublishFrameAsync(CanFrame frame)
    {
        if (!frame.IsValid())
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(frame);
    }

    public void SetActiveDbc(DbcDatabase? dbc)
    {
        lock (_dbcLock)
        {
            _activeDbc = dbc;
            if (dbc != null)
            {
                _topologyEngine.LoadDbc(dbc);
            }
        }
    }

    public void ResetDefaultDbc()
    {
        SetActiveDbc(GetDefaultMarelliSwiftDbc());
    }

    public DbcDatabase? GetActiveDbc()
    {
        lock (_dbcLock)
        {
            return _activeDbc;
        }
    }

    public static DbcDatabase GetDefaultMarelliSwiftDbc()
    {
        const string dbcContent = @"VERSION ""1.0""

BU_: ECM ABS BCM CLUSTER

BO_ 292 Engine_0x124: 8 ECM
 SG_ EngineRPM : 0|16@1+ (0.25,0) [0|8000] ""rpm"" CLUSTER,ABS
 SG_ ThrottlePosition : 16|8@1+ (0.392157,0) [0|100] ""%"" CLUSTER
 SG_ EngineTorque : 24|8@1+ (1,-100) [-100|400] ""Nm"" ABS
 SG_ EngineStatus : 32|8@1+ (1,0) [0|255] """" CLUSTER
 SG_ AcceleratorPedal : 40|8@1+ (0.392157,0) [0|100] ""%"" CLUSTER

BO_ 784 Speed_0x310: 8 ABS
 SG_ VehicleSpeed : 0|16@1+ (0.01,0) [0|250] ""km/h"" ECM,CLUSTER
 SG_ BrakeSwitch : 16|1@1+ (1,0) [0|1] """" ECM
 SG_ ClutchSwitch : 17|1@1+ (1,0) [0|1] """" ECM
 SG_ WheelSpeedFL : 24|16@1+ (0.01,0) [0|250] ""km/h"" CLUSTER
 SG_ WheelSpeedFR : 40|16@1+ (0.01,0) [0|250] ""km/h"" CLUSTER

BO_ 788 Thermal_0x314: 8 ECM
 SG_ EngineCoolantTemp : 0|8@1+ (1,-40) [-40|150] ""degC"" CLUSTER
 SG_ FuelTemperature : 8|8@1+ (1,-40) [-40|120] ""degC"" CLUSTER
 SG_ AmbientAirTemp : 16|8@1+ (0.5,-40) [-40|85] ""degC"" CLUSTER
 SG_ OilTemperature : 24|8@1+ (1,-40) [-40|160] ""degC"" CLUSTER

BO_ 792 Pressure_Relays_0x318: 8 ECM
 SG_ FuelRailPressure : 0|16@1+ (0.1,0) [0|2000] ""bar"" CLUSTER
 SG_ BoostPressure : 16|16@1+ (0.1,0) [0|350] ""kPa"" CLUSTER
 SG_ AcRelayStatus : 32|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ RadiatorFanRelay : 33|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ GlowPlugRelay : 34|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ BatteryVoltage : 40|8@1+ (0.1,0) [0|25] ""V"" CLUSTER

BO_ 1852 BCM_Immo_0x73C: 8 BCM
 SG_ ImmobilizerStatus : 0|8@1+ (1,0) [0|255] """" ECM
 SG_ KeyStatus : 8|8@1+ (1,0) [0|255] """" ECM
 SG_ BodyWarningState : 16|8@1+ (1,0) [0|255] """" ECM

BO_ 280 EngineData_Sim: 8 ECM
 SG_ EngineSpeed_Sim : 0|16@1+ (0.125,0) [0|8000] ""rpm"" CLUSTER,BCM,ABS
 SG_ EngineTorque_Sim : 16|8@1+ (1,-100) [-100|155] ""Nm"" ABS
 SG_ ThrottlePosition_Sim : 24|8@1+ (0.392,0) [0|100] ""%"" CLUSTER
 SG_ EngineCoolantTemp_Sim : 32|8@1+ (1,-40) [-40|215] ""degC"" CLUSTER
 SG_ FuelRailPressure_Sim : 40|16@1+ (0.1,0) [0|3000] ""bar"" CLUSTER

BO_ 390 VehicleDynamics_Sim: 8 ABS
 SG_ VehicleSpeed_Sim : 0|16@1+ (0.01,0) [0|250] ""km/h"" ECM,CLUSTER,BCM
 SG_ BrakePressure_Sim : 16|8@1+ (1,0) [0|250] ""bar"" ECM
 SG_ AbsActive_Sim : 44|1@1+ (1,0) [0|1] """" CLUSTER
";
        return DbcParser.Parse(dbcContent);
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new CanFrameBatch();
        var batchTimer = DateTime.UtcNow;
        var reader = _channel.Reader;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Read up to 50 frames or wait up to 20ms for batching
                while (batch.Frames.Count < 50 && reader.TryRead(out var frame))
                {
                    ProcessSingleFrame(frame, batch);
                }

                if (batch.Frames.Count == 0)
                {
                    if (await reader.WaitToReadAsync(_cts.Token))
                    {
                        if (reader.TryRead(out var frame))
                        {
                            ProcessSingleFrame(frame, batch);
                        }
                    }
                }

                var now = DateTime.UtcNow;
                if (batch.Frames.Count > 0 && (batch.Frames.Count >= 20 || (now - batchTimer).TotalMilliseconds >= 25))
                {
                    batch.FrameRate = _currentFps;
                    OnBatchProcessed?.Invoke(batch);

                    batch = new CanFrameBatch();
                    batchTimer = now;
                }

                // Update FPS calculation once per second
                var fpsElapsed = (now - _lastFpsCalc).TotalSeconds;
                if (fpsElapsed >= 1.0)
                {
                    _currentFps = Math.Round(_windowFrames / fpsElapsed, 1);
                    _windowFrames = 0;
                    _lastFpsCalc = now;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log and continue — pipeline must not crash on transient frame-processing errors
                System.Diagnostics.Debug.WriteLine($"[CanPipeline] Unhandled exception in processing loop: {ex}");
            }
        }
    }

    private void ProcessSingleFrame(CanFrame frame, CanFrameBatch batch)
    {
        Interlocked.Increment(ref _totalFrames);
        _windowFrames++;

        batch.Frames.Add(frame);
        OnFrameReceived?.Invoke(frame);

        _loggerService.ProcessFrame(frame);

        DbcMessage? matchedMsg = null;
        List<DecodedSignal>? decodedSignals = null;

        lock (_dbcLock)
        {
            if (_activeDbc != null && _activeDbc.MessageLookup.TryGetValue(frame.Id & 0x1FFFFFFFU, out matchedMsg))
            {
                decodedSignals = SignalDecoder.DecodeFrame(frame, matchedMsg);
            }
        }

        if (decodedSignals != null && decodedSignals.Count > 0)
        {
            batch.Signals.AddRange(decodedSignals);
            OnSignalsDecoded?.Invoke(decodedSignals);
        }

        _topologyEngine.ProcessFrame(frame, matchedMsg, decodedSignals);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
