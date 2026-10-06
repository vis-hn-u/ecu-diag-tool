using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware;

public enum TransportType
{
    Esp8266,
    WaveshareCanFd,
    Simulation,
    Elm327,
    Esp8266UsbSerial
}

public class HardwareCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Can;
    public bool SupportsCanFd { get; init; }
    public bool SupportsBitRateSwitch { get; init; }
    public bool SupportsClassicCan { get; init; } = true;
    public int ChannelCount { get; init; } = 1;
    public int MaxPayloadBytes { get; init; } = 8;
    public bool SupportsTransmit { get; init; }
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public IReadOnlyList<uint> SupportedNominalBaudRates { get; init; } = Array.Empty<uint>();
    public IReadOnlyList<uint> SupportedDataBaudRates { get; init; } = Array.Empty<uint>();
}

public class HardwareStatus
{
    public string TransportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TransportType Type { get; set; }
    public bool IsConnected { get; set; }
    public string ConnectionDetails { get; set; } = string.Empty;
    public long TotalFramesReceived { get; set; }
    public long TotalFramesTransmitted { get; set; }
    public double RxFrameRate { get; set; }
    public double TxFrameRate { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string? LastError { get; set; }
    public HardwareCapabilities Capabilities { get; set; } = new();
    public object? ExtendedTelemetry { get; set; }
}
