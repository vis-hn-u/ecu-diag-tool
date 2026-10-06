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

    public DbcDatabase? GetActiveDbc()
    {
        lock (_dbcLock)
        {
            return _activeDbc;
        }
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
