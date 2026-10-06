using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Simulation.DigitalTwin;

public enum DigitalTwinEcuType
{
    Ecm,     // Engine Control Module
    Tcm,     // Transmission Control Module
    Abs,     // Anti-Lock Braking System
    Bcm,     // Body Control Module
    Eps,     // Electronic Power Steering
    Hvac,    // Climate Control
    Cluster, // Instrument Cluster
    Gateway, // Central Gateway
    Adas,    // Advanced Driver Assistance System
    Bms,     // Battery Management System
    Obc,     // On-Board Charger
    Inverter // Traction Inverter
}

public class DigitalTwinNode
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DigitalTwinEcuType Type { get; set; }
    public bool IsOnline { get; set; } = true;
    public uint RequestCanId { get; set; }
    public uint ResponseCanId { get; set; }
    public List<uint> BroadcastCanIds { get; set; } = new();
    public int BroadcastIntervalMs { get; set; } = 20;
    public double Voltage { get; set; } = 13.8;
    public List<string> ActiveDtcs { get; set; } = new();
    public Dictionary<string, double> SimulatedSignals { get; set; } = new();
    public bool InjectedBusDrop { get; set; } = false;
}

public class DigitalTwinFaultRequest
{
    public string NodeId { get; set; } = "ECM";
    public string? DtcCode { get; set; }
    public bool SetDtcActive { get; set; } = true;
    public double? VoltageOverride { get; set; }
    public bool? BusDrop { get; set; }
    public string? SignalName { get; set; }
    public double? SignalValue { get; set; }
}

public interface IVehicleDigitalTwinService : IDisposable
{
    IReadOnlyList<DigitalTwinNode> GetNodes();
    DigitalTwinNode? GetNode(string nodeId);
    void SetFault(DigitalTwinFaultRequest fault);
    void ClearFaults(string? nodeId = null);
    void StartSimulation();
    void StopSimulation();
    bool IsRunning { get; }
    int ActiveNodeCount { get; }
}

public class VehicleDigitalTwinService : IVehicleDigitalTwinService
{
    private readonly ICanPipeline _pipeline;
    private readonly ConcurrentDictionary<string, DigitalTwinNode> _nodes = new();
    private CancellationTokenSource? _simulationCts;
    private Task? _simulationTask;
    private bool _disposed;

    public bool IsRunning => _simulationTask != null && !_simulationTask.IsCompleted;
    public int ActiveNodeCount => _nodes.Values.Count(n => n.IsOnline && !n.InjectedBusDrop);

    public VehicleDigitalTwinService(ICanPipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        InitializeDefaultTwinNodes();
    }

    private void InitializeDefaultTwinNodes()
    {
        // 1. ECM
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "ECM",
            Name = "Engine Control Module",
            Type = DigitalTwinEcuType.Ecm,
            RequestCanId = 0x7E0,
            ResponseCanId = 0x7E8,
            BroadcastCanIds = new() { 0x280, 0x288, 0x380 },
            BroadcastIntervalMs = 10,
            SimulatedSignals = new()
            {
                ["ENGINE_RPM"] = 850.0,
                ["COOLANT_TEMP"] = 92.0,
                ["THROTTLE_POS"] = 14.5,
                ["VEHICLE_SPEED"] = 0.0,
                ["MODULE_VOLTAGE"] = 13.8
            }
        });

        // 2. TCM
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "TCM",
            Name = "Transmission Control Module",
            Type = DigitalTwinEcuType.Tcm,
            RequestCanId = 0x7E1,
            ResponseCanId = 0x7E9,
            BroadcastCanIds = new() { 0x440, 0x448 },
            BroadcastIntervalMs = 20,
            SimulatedSignals = new()
            {
                ["TRANS_GEAR"] = 1.0,
                ["TRANS_FLUID_TEMP"] = 80.0,
                ["TORQUE_CONVERTER_SLIP"] = 20.0
            }
        });

        // 3. ABS / ESC
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "ABS",
            Name = "Anti-Lock Braking & Stability",
            Type = DigitalTwinEcuType.Abs,
            RequestCanId = 0x7E2,
            ResponseCanId = 0x7EA,
            BroadcastCanIds = new() { 0x1A0, 0x1A8 },
            BroadcastIntervalMs = 10,
            SimulatedSignals = new()
            {
                ["WHEEL_SPEED_FL"] = 0.0,
                ["WHEEL_SPEED_FR"] = 0.0,
                ["WHEEL_SPEED_RL"] = 0.0,
                ["WHEEL_SPEED_RR"] = 0.0,
                ["BRAKE_PRESSURE_BAR"] = 0.0
            }
        });

        // 4. BCM
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "BCM",
            Name = "Body Control Module",
            Type = DigitalTwinEcuType.Bcm,
            RequestCanId = 0x7E3,
            ResponseCanId = 0x7EB,
            BroadcastCanIds = new() { 0x500, 0x508 },
            BroadcastIntervalMs = 100,
            SimulatedSignals = new()
            {
                ["IGNITION_STATE"] = 2.0, // Key ON
                ["DOOR_OPEN"] = 0.0,
                ["HEADLIGHTS"] = 0.0
            }
        });

        // 5. EPS
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "EPS",
            Name = "Electronic Power Steering",
            Type = DigitalTwinEcuType.Eps,
            RequestCanId = 0x7E4,
            ResponseCanId = 0x7EC,
            BroadcastCanIds = new() { 0x0C2 },
            BroadcastIntervalMs = 10,
            SimulatedSignals = new()
            {
                ["STEERING_ANGLE_DEG"] = 0.0,
                ["STEERING_TORQUE_NM"] = 0.0
            }
        });

        // 6. Cluster
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "CLUSTER",
            Name = "Instrument Cluster",
            Type = DigitalTwinEcuType.Cluster,
            RequestCanId = 0x7E5,
            ResponseCanId = 0x7ED,
            BroadcastCanIds = new() { 0x5A0 },
            BroadcastIntervalMs = 100,
            SimulatedSignals = new()
            {
                ["ODOMETER_KM"] = 45210.0,
                ["FUEL_LEVEL_PCT"] = 72.0
            }
        });

        // 7. Gateway
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "GATEWAY",
            Name = "Central CAN/CAN-FD Gateway",
            Type = DigitalTwinEcuType.Gateway,
            RequestCanId = 0x7E6,
            ResponseCanId = 0x7EE,
            BroadcastCanIds = new() { 0x000, 0x100 },
            BroadcastIntervalMs = 50,
            SimulatedSignals = new()
            {
                ["BUS_LOAD_PCT"] = 28.5,
                ["GATEWAY_UPTIME_SEC"] = 3600.0
            }
        });

        // 8. ADAS
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "ADAS",
            Name = "Driver Assistance Radar/Camera",
            Type = DigitalTwinEcuType.Adas,
            RequestCanId = 0x7E7,
            ResponseCanId = 0x7EF,
            BroadcastCanIds = new() { 0x2B0 },
            BroadcastIntervalMs = 20,
            SimulatedSignals = new()
            {
                ["FORWARD_COLLISION_WARNING"] = 0.0,
                ["LANE_DEPARTURE_ACTIVE"] = 0.0
            }
        });

        // 9. BMS (Hybrid/EV)
        RegisterNode(new DigitalTwinNode
        {
            NodeId = "BMS",
            Name = "Battery Management System",
            Type = DigitalTwinEcuType.Bms,
            RequestCanId = 0x710,
            ResponseCanId = 0x718,
            BroadcastCanIds = new() { 0x110, 0x112 },
            BroadcastIntervalMs = 50,
            SimulatedSignals = new()
            {
                ["TRACTION_SOC_PCT"] = 84.0,
                ["TRACTION_VOLTAGE_V"] = 385.2,
                ["MAX_CELL_TEMP_C"] = 29.5
            }
        });
    }

    private void RegisterNode(DigitalTwinNode node)
    {
        _nodes[node.NodeId] = node;
    }

    public IReadOnlyList<DigitalTwinNode> GetNodes() => _nodes.Values.ToList();

    public DigitalTwinNode? GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;
        _nodes.TryGetValue(nodeId.ToUpperInvariant(), out var node);
        return node;
    }

    public void SetFault(DigitalTwinFaultRequest fault)
    {
        if (fault == null) return;
        var node = GetNode(fault.NodeId);
        if (node == null) return;

        if (!string.IsNullOrEmpty(fault.DtcCode))
        {
            if (fault.SetDtcActive && !node.ActiveDtcs.Contains(fault.DtcCode))
            {
                node.ActiveDtcs.Add(fault.DtcCode);
            }
            else if (!fault.SetDtcActive)
            {
                node.ActiveDtcs.Remove(fault.DtcCode);
            }
        }

        if (fault.VoltageOverride.HasValue)
        {
            node.Voltage = fault.VoltageOverride.Value;
        }

        if (fault.BusDrop.HasValue)
        {
            node.InjectedBusDrop = fault.BusDrop.Value;
        }

        if (!string.IsNullOrEmpty(fault.SignalName) && fault.SignalValue.HasValue)
        {
            node.SimulatedSignals[fault.SignalName] = fault.SignalValue.Value;
        }
    }

    public void ClearFaults(string? nodeId = null)
    {
        if (!string.IsNullOrEmpty(nodeId))
        {
            var node = GetNode(nodeId);
            if (node != null)
            {
                node.ActiveDtcs.Clear();
                node.InjectedBusDrop = false;
                node.Voltage = 13.8;
            }
        }
        else
        {
            foreach (var node in _nodes.Values)
            {
                node.ActiveDtcs.Clear();
                node.InjectedBusDrop = false;
                node.Voltage = 13.8;
            }
        }
    }

    public void StartSimulation()
    {
        if (IsRunning) return;

        _simulationCts = new CancellationTokenSource();
        var token = _simulationCts.Token;

        _simulationTask = Task.Run(async () =>
        {
            ulong tick = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    tick++;

                    // Broadcast cyclic frames for online nodes
                    foreach (var node in _nodes.Values)
                    {
                        if (!node.IsOnline || node.InjectedBusDrop) continue;

                        if (tick % (ulong)Math.Max(1, node.BroadcastIntervalMs / 10) == 0)
                        {
                            foreach (var canId in node.BroadcastCanIds)
                            {
                                byte[] payload = GenerateNodePayload(node, canId, tick);
                                var frame = new CanFrame(0, canId, (byte)payload.Length, 0, payload);
                                await _pipeline.PublishFrameAsync(frame);
                            }
                        }
                    }

                    await Task.Delay(10, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(50, token);
                }
            }
        }, token);
    }

    private static byte[] GenerateNodePayload(DigitalTwinNode node, uint canId, ulong tick)
    {
        byte[] payload = new byte[8];
        byte counter = (byte)(tick & 0x0F);

        switch (node.Type)
        {
            case DigitalTwinEcuType.Ecm:
                // RPM in bytes 2-3 (val * 4)
                double rpm = node.SimulatedSignals.GetValueOrDefault("ENGINE_RPM", 850.0);
                ushort rawRpm = (ushort)(rpm * 4.0);
                payload[0] = counter;
                payload[1] = 0x00;
                payload[2] = (byte)(rawRpm >> 8);
                payload[3] = (byte)(rawRpm & 0xFF);
                payload[4] = (byte)(node.SimulatedSignals.GetValueOrDefault("COOLANT_TEMP", 90.0) + 40.0);
                payload[5] = (byte)node.SimulatedSignals.GetValueOrDefault("THROTTLE_POS", 15.0);
                payload[6] = 0x00;
                payload[7] = (byte)((payload[0] + payload[2] + payload[3]) ^ 0xFF); // Checksum
                break;

            case DigitalTwinEcuType.Abs:
                // Wheel speed
                payload[0] = counter;
                payload[1] = (byte)node.SimulatedSignals.GetValueOrDefault("WHEEL_SPEED_FL", 0.0);
                payload[2] = (byte)node.SimulatedSignals.GetValueOrDefault("WHEEL_SPEED_FR", 0.0);
                payload[3] = (byte)node.SimulatedSignals.GetValueOrDefault("WHEEL_SPEED_RL", 0.0);
                payload[4] = (byte)node.SimulatedSignals.GetValueOrDefault("WHEEL_SPEED_RR", 0.0);
                break;

            default:
                payload[0] = counter;
                payload[7] = 0xAA;
                break;
        }

        return payload;
    }

    public void StopSimulation()
    {
        if (_simulationCts != null)
        {
            _simulationCts.Cancel();
            _simulationCts.Dispose();
            _simulationCts = null;
        }
        _simulationTask = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopSimulation();
    }
}
