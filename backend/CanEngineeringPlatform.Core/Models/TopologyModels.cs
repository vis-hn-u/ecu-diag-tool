using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Models;

public enum TopologyMode
{
    Live,
    Paused,
    Simulation
}

public class NetworkNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = "ECU"; // ECU, Gateway, Sensor, Actuator, Unknown
    public string Status { get; set; } = "Active"; // Active, Inactive
    public string Source { get; set; } = "Observed"; // Dbc, Observed
    public DateTime? LastSeenUtc { get; set; }
    public long MessageCount { get; set; }
    public double TxRateHz { get; set; }
}

public class CanMessageInfo
{
    public uint Id { get; set; }
    public string HexId => "0x" + Id.ToString("X3");
    public string Name { get; set; } = string.Empty;
    public string Transmitter { get; set; } = string.Empty;
    public List<string> Receivers { get; set; } = new();
    public byte Dlc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public double FrequencyHz { get; set; }
    public long FrameCount { get; set; }
    public List<string> Signals { get; set; } = new();
    public bool IsKnown { get; set; } = true;
}

public class TopologyEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public uint MessageId { get; set; }
    public string MessageName { get; set; } = string.Empty;
    public string SignalName { get; set; } = string.Empty;
    public DateTime? LastSeenUtc { get; set; }
    public string State { get; set; } = "Active"; // Active, Idle
    public double UpdateRateHz { get; set; }
}

public class NetworkTopologyState
{
    public List<NetworkNode> Nodes { get; set; } = new();
    public List<CanMessageInfo> Messages { get; set; } = new();
    public List<TopologyEdge> Edges { get; set; } = new();
    public TopologyMode Mode { get; set; } = TopologyMode.Live;
    public long TotalFramesObserved { get; set; }
    public int ActiveNodeCount { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
