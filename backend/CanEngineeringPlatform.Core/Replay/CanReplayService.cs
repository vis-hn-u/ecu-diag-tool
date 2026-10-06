using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Replay;

public class ReplayStatus
{
    public bool IsLoaded { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsPaused { get; set; }
    public int CurrentIndex { get; set; }
    public int TotalFrames { get; set; }
    public double SpeedMultiplier { get; set; } = 1.0;
    public string Name { get; set; } = string.Empty;
    public double ProgressPercent => TotalFrames > 0 ? Math.Round((double)CurrentIndex / TotalFrames * 100.0, 1) : 0.0;
}

public interface ICanReplayService
{
    void LoadFrames(IReadOnlyList<CanFrame> frames, string name);
    bool LoadFromCsv(string csvContent, string name);
    void Play();
    void Pause();
    void Stop();
    void Seek(int index);
    void SetSpeed(double multiplier);
    ReplayStatus GetStatus();
}

public class CanReplayService : ICanReplayService, IDisposable
{
    private readonly ICanPipeline _pipeline;
    private readonly object _lock = new();
    private List<CanFrame> _frames = new();
    private string _name = string.Empty;
    private int _currentIndex;
    private double _speedMultiplier = 1.0;
    private bool _isPlaying;
    private bool _isPaused;
    private CancellationTokenSource? _cts;
    private Task? _playbackTask;

    public CanReplayService(ICanPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public void LoadFrames(IReadOnlyList<CanFrame> frames, string name)
    {
        lock (_lock)
        {
            Stop();
            _frames = new List<CanFrame>(frames);
            _name = name;
            _currentIndex = 0;
        }
    }

    public bool LoadFromCsv(string csvContent, string name)
    {
        try
        {
            var frames = new List<CanFrame>();
            using var reader = new StringReader(csvContent);
            string? header = reader.ReadLine();
            if (header == null) return false;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                // Format: TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex
                uint ts = uint.Parse(parts[0], CultureInfo.InvariantCulture);
                uint id = parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt32(parts[1], 16)
                    : uint.Parse(parts[2], CultureInfo.InvariantCulture);
                bool isExtended = parts[3].Equals("Extended", StringComparison.OrdinalIgnoreCase) || id > 0x7FF;
                bool isRtr = parts[4] == "1" || parts[4].Equals("true", StringComparison.OrdinalIgnoreCase);
                byte dlc = byte.Parse(parts[5], CultureInfo.InvariantCulture);

                byte flags = 0;
                if (isExtended) flags |= CanFrame.FlagExtended;
                if (isRtr) flags |= CanFrame.FlagRtr;

                byte channel = 0;
                string sourceId = "replay";
                bool isFd = dlc > 8;

                if (parts.Length >= 8 && byte.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte ch))
                {
                    channel = ch;
                }
                if (parts.Length >= 9 && parts[8].Contains("FD", StringComparison.OrdinalIgnoreCase))
                {
                    isFd = true;
                }
                if (parts.Length >= 10 && (parts[9] == "1" || parts[9].Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    flags |= CanFrame.FlagBrs;
                }
                if (parts.Length >= 11 && (parts[10] == "1" || parts[10].Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    flags |= CanFrame.FlagEsi;
                }
                if (parts.Length >= 12 && !string.IsNullOrWhiteSpace(parts[11]))
                {
                    sourceId = parts[11].Trim();
                }

                byte[] data = Array.Empty<byte>();
                if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]))
                {
                    var hexParts = parts[6].Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (hexParts.Length > 8)
                    {
                        isFd = true;
                    }

                    int maxLen = isFd ? Math.Min(hexParts.Length, 64) : Math.Min(hexParts.Length, 8);
                    data = new byte[maxLen];
                    for (int i = 0; i < maxLen; i++)
                    {
                        data[i] = Convert.ToByte(hexParts[i], 16);
                    }
                }

                if (isFd)
                {
                    flags |= CanFrame.FlagCanFd;
                }

                frames.Add(new CanFrame(ts, id, dlc, flags, data, channel, sourceId));
            }

            if (frames.Count > 0)
            {
                LoadFrames(frames, name);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Play()
    {
        lock (_lock)
        {
            if (_frames.Count == 0) return;
            if (_isPlaying && !_isPaused) return;

            if (_isPaused)
            {
                _isPaused = false;
                return;
            }

            _isPlaying = true;
            _isPaused = false;
            _cts = new CancellationTokenSource();
            _playbackTask = Task.Run(() => PlaybackLoopAsync(_cts.Token));
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_isPlaying)
            {
                _isPaused = true;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _isPlaying = false;
            _isPaused = false;
            _currentIndex = 0;
        }
    }

    public void Seek(int index)
    {
        lock (_lock)
        {
            if (_frames.Count == 0) return;
            _currentIndex = Math.Clamp(index, 0, _frames.Count - 1);
        }
    }

    public void SetSpeed(double multiplier)
    {
        lock (_lock)
        {
            _speedMultiplier = Math.Clamp(multiplier, 0.1, 20.0);
        }
    }

    public ReplayStatus GetStatus()
    {
        lock (_lock)
        {
            return new ReplayStatus
            {
                IsLoaded = _frames.Count > 0,
                IsPlaying = _isPlaying && !_isPaused,
                IsPaused = _isPaused,
                CurrentIndex = _currentIndex,
                TotalFrames = _frames.Count,
                SpeedMultiplier = _speedMultiplier,
                Name = _name
            };
        }
    }

    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            CanFrame frame;
            double speed;
            bool isPaused;
            int total;

            lock (_lock)
            {
                isPaused = _isPaused;
                if (_currentIndex >= _frames.Count)
                {
                    _isPlaying = false;
                    _isPaused = false;
                    _currentIndex = 0;
                    break;
                }
                frame = _frames[_currentIndex];
                speed = _speedMultiplier;
                total = _frames.Count;
            }

            if (isPaused)
            {
                await Task.Delay(50, ct);
                continue;
            }

            // Publish frame to pipeline in simulation mode
            await _pipeline.PublishFrameAsync(frame);

            int nextIndex;
            lock (_lock)
            {
                _currentIndex++;
                nextIndex = _currentIndex;
            }

            if (nextIndex < total)
            {
                uint nextTs = _frames[nextIndex].TimestampUs;
                long deltaUs = nextTs >= frame.TimestampUs ? (nextTs - frame.TimestampUs) : 1000;
                double delayMs = (deltaUs / 1000.0) / speed;

                if (delayMs > 0.5)
                {
                    await Task.Delay(Math.Min((int)delayMs, 1000), ct);
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
