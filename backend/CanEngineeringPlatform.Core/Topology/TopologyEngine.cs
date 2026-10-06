using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Topology;

public interface ITopologyEngine
{
    void LoadDbc(DbcDatabase dbc);
    void ProcessFrame(CanFrame frame, DbcMessage? matchedMessage, IReadOnlyList<DecodedSignal>? signals);
    NetworkTopologyState GetSnapshot();
    void SetMode(TopologyMode mode);
    void Reset();
}

public class TopologyEngine : ITopologyEngine
{
    private readonly object _lock = new();
    private DbcDatabase? _activeDbc;
    private TopologyMode _mode = TopologyMode.Live;
    private long _totalFrames;

    private class NodeStats
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "ECU";
        public string Source { get; set; } = "Observed";
        public DateTime? LastSeenUtc { get; set; }
        public long MessageCount { get; set; }
        public int WindowCount { get; set; }
        public DateTime LastRateCalcUtc { get; set; } = DateTime.UtcNow;
        public double CurrentRateHz { get; set; }
    }

    private class MessageStats
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Transmitter { get; set; } = string.Empty;
        public HashSet<string> Receivers { get; set; } = new();
        public byte Dlc { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public long FrameCount { get; set; }
        public int WindowCount { get; set; }
        public DateTime LastRateCalcUtc { get; set; } = DateTime.UtcNow;
        public double CurrentFrequencyHz { get; set; }
        public HashSet<string> Signals { get; set; } = new();
        public bool IsKnown { get; set; } = true;
    }

    private class EdgeStats
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public uint MessageId { get; set; }
        public string MessageName { get; set; } = string.Empty;
        public string SignalName { get; set; } = string.Empty;
        public DateTime? LastSeenUtc { get; set; }
        public int WindowCount { get; set; }
        public DateTime LastRateCalcUtc { get; set; } = DateTime.UtcNow;
        public double CurrentRateHz { get; set; }
    }

    private readonly ConcurrentDictionary<string, NodeStats> _nodes = new();
    private readonly ConcurrentDictionary<uint, MessageStats> _messages = new();
    private readonly ConcurrentDictionary<string, EdgeStats> _edges = new();

    public void LoadDbc(DbcDatabase dbc)
    {
        lock (_lock)
        {
            _activeDbc = dbc;

            // Pre-seed DBC nodes
            foreach (var node in dbc.Nodes)
            {
                if (node.Name == "Vector__XXX" || string.IsNullOrWhiteSpace(node.Name)) continue;
                _nodes.GetOrAdd(node.Name, n => new NodeStats
                {
                    Id = n,
                    Name = n,
                    Type = DetermineNodeType(n),
                    Source = "Dbc"
                });
            }

            // Pre-seed DBC messages and edges
            foreach (var msg in dbc.Messages)
            {
                var cleanId = msg.CleanId;
                var msgStats = _messages.GetOrAdd(cleanId, id => new MessageStats
                {
                    Id = id,
                    Name = msg.Name,
                    Transmitter = string.IsNullOrWhiteSpace(msg.Transmitter) || msg.Transmitter == "Vector__XXX" ? "Unknown" : msg.Transmitter,
                    Dlc = msg.Dlc,
                    IsKnown = true
                });

                if (!string.IsNullOrWhiteSpace(msg.Transmitter) && msg.Transmitter != "Vector__XXX")
                {
                    _nodes.GetOrAdd(msg.Transmitter, n => new NodeStats
                    {
                        Id = n,
                        Name = n,
                        Type = DetermineNodeType(n),
                        Source = "Dbc"
                    });
                }

                foreach (var sig in msg.Signals)
                {
                    msgStats.Signals.Add(sig.Name);
                    foreach (var receiver in sig.Receivers)
                    {
                        if (string.IsNullOrWhiteSpace(receiver) || receiver == "Vector__XXX") continue;
                        msgStats.Receivers.Add(receiver);

                        _nodes.GetOrAdd(receiver, n => new NodeStats
                        {
                            Id = n,
                            Name = n,
                            Type = DetermineNodeType(n),
                            Source = "Dbc"
                        });

                        string edgeKey = $"{msgStats.Transmitter}->{receiver}:{cleanId}:{sig.Name}";
                        _edges.GetOrAdd(edgeKey, k => new EdgeStats
                        {
                            Source = msgStats.Transmitter,
                            Target = receiver,
                            MessageId = cleanId,
                            MessageName = msg.Name,
                            SignalName = sig.Name
                        });
                    }
                }
            }
        }
    }

    public void ProcessFrame(CanFrame frame, DbcMessage? matchedMessage, IReadOnlyList<DecodedSignal>? signals)
    {
        if (_mode == TopologyMode.Paused)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        uint cleanId = frame.Id & 0x1FFFFFFFU;
        byte dlc = frame.Dlc;
        System.Threading.Interlocked.Increment(ref _totalFrames);

        string transmitter = "UNKNOWN_NODE";
        string messageName = $"UNKNOWN (0x{cleanId:X3})";
        bool isKnown = false;

        if (matchedMessage != null)
        {
            isKnown = true;
            transmitter = string.IsNullOrWhiteSpace(matchedMessage.Transmitter) || matchedMessage.Transmitter == "Vector__XXX"
                ? "ECU_UNKNOWN"
                : matchedMessage.Transmitter;
            messageName = matchedMessage.Name;
        }

        // Update / Add Node
        var node = _nodes.GetOrAdd(transmitter, n => new NodeStats
        {
            Id = n,
            Name = n,
            Type = DetermineNodeType(n),
            Source = isKnown ? "Dbc" : "Observed"
        });

        lock (node)
        {
            node.LastSeenUtc = now;
            node.MessageCount++;
            node.WindowCount++;
            var elapsed = (now - node.LastRateCalcUtc).TotalSeconds;
            if (elapsed >= 1.0)
            {
                node.CurrentRateHz = node.WindowCount / elapsed;
                node.WindowCount = 0;
                node.LastRateCalcUtc = now;
            }
        }

        // Update / Add Message
        var msg = _messages.GetOrAdd(cleanId, id => new MessageStats
        {
            Id = id,
            Name = messageName,
            Transmitter = transmitter,
            Dlc = dlc,
            IsKnown = isKnown
        });

        lock (msg)
        {
            msg.LastSeenUtc = now;
            msg.FrameCount++;
            msg.WindowCount++;
            msg.Dlc = frame.Dlc;
            msg.IsKnown = isKnown;
            msg.Name = messageName;
            msg.Transmitter = transmitter;

            var elapsed = (now - msg.LastRateCalcUtc).TotalSeconds;
            if (elapsed >= 1.0)
            {
                msg.CurrentFrequencyHz = msg.WindowCount / elapsed;
                msg.WindowCount = 0;
                msg.LastRateCalcUtc = now;
            }
        }

        // Update / Add Edges from decoded signals
        if (signals != null && signals.Count > 0)
        {
            foreach (var sig in signals)
            {
                foreach (var receiver in sig.ReceiverNodes)
                {
                    if (string.IsNullOrWhiteSpace(receiver) || receiver == "Vector__XXX") continue;

                    string edgeKey = $"{transmitter}->{receiver}:{cleanId}:{sig.SignalName}";
                    var edge = _edges.GetOrAdd(edgeKey, k => new EdgeStats
                    {
                        Source = transmitter,
                        Target = receiver,
                        MessageId = cleanId,
                        MessageName = messageName,
                        SignalName = sig.SignalName
                    });

                    lock (edge)
                    {
                        edge.LastSeenUtc = now;
                        edge.WindowCount++;
                        var elapsed = (now - edge.LastRateCalcUtc).TotalSeconds;
                        if (elapsed >= 1.0)
                        {
                            edge.CurrentRateHz = edge.WindowCount / elapsed;
                            edge.WindowCount = 0;
                            edge.LastRateCalcUtc = now;
                        }
                    }
                }
            }
        }
    }

    public NetworkTopologyState GetSnapshot()
    {
        DateTime now = DateTime.UtcNow;
        var snapshot = new NetworkTopologyState
        {
            Mode = _mode,
            TotalFramesObserved = _totalFrames,
            LastUpdatedUtc = now
        };

        foreach (var kvp in _nodes)
        {
            var n = kvp.Value;
            bool isActive = n.LastSeenUtc.HasValue && (now - n.LastSeenUtc.Value).TotalSeconds <= 3.0;
            snapshot.Nodes.Add(new NetworkNode
            {
                Id = n.Id,
                Name = n.Name,
                Type = n.Type,
                Status = isActive ? "Active" : "Inactive",
                Source = n.Source,
                LastSeenUtc = n.LastSeenUtc,
                MessageCount = n.MessageCount,
                TxRateHz = Math.Round(n.CurrentRateHz, 1)
            });
        }

        snapshot.ActiveNodeCount = snapshot.Nodes.Count(n => n.Status == "Active");

        foreach (var kvp in _messages)
        {
            var m = kvp.Value;
            snapshot.Messages.Add(new CanMessageInfo
            {
                Id = m.Id,
                Name = m.Name,
                Transmitter = m.Transmitter,
                Receivers = m.Receivers.ToList(),
                Dlc = m.Dlc,
                LastSeenUtc = m.LastSeenUtc,
                FrequencyHz = Math.Round(m.CurrentFrequencyHz, 1),
                FrameCount = m.FrameCount,
                Signals = m.Signals.ToList(),
                IsKnown = m.IsKnown
            });
        }

        foreach (var kvp in _edges)
        {
            var e = kvp.Value;
            bool isActive = e.LastSeenUtc.HasValue && (now - e.LastSeenUtc.Value).TotalSeconds <= 3.0;
            snapshot.Edges.Add(new TopologyEdge
            {
                Source = e.Source,
                Target = e.Target,
                MessageId = e.MessageId,
                MessageName = e.MessageName,
                SignalName = e.SignalName,
                LastSeenUtc = e.LastSeenUtc,
                State = isActive ? "Active" : "Idle",
                UpdateRateHz = Math.Round(e.CurrentRateHz, 1)
            });
        }

        return snapshot;
    }

    public void SetMode(TopologyMode mode)
    {
        _mode = mode;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _messages.Clear();
            _edges.Clear();
            _totalFrames = 0;
            if (_activeDbc != null)
            {
                LoadDbc(_activeDbc);
            }
        }
    }

    private static string DetermineNodeType(string name)
    {
        string upper = name.ToUpperInvariant();
        if (upper.Contains("GW") || upper.Contains("GATEWAY")) return "Gateway";
        if (upper.Contains("SENSOR") || upper.Contains("RADAR") || upper.Contains("CAMERA")) return "Sensor";
        if (upper.Contains("ACTUATOR") || upper.Contains("MOTOR") || upper.Contains("VALVE")) return "Actuator";
        if (upper.Contains("UNKNOWN")) return "Unknown";
        return "ECU";
    }
}
