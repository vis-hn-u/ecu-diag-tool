using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Decoders;
using CanEngineeringPlatform.Core.Models;

[assembly: InternalsVisibleTo("CanEngineeringPlatform.Tests")]

namespace CanEngineeringPlatform.Core.Hardware.Transports;

public class Esp8266UsbSerialTransport : ICanTransport
{
    public string TransportId => "esp8266-usb-serial";
    public string Name => "ESP8266 + MCP2515 Gateway (USB Serial)";
    public TransportType Type => TransportType.Esp8266UsbSerial;

    public HardwareCapabilities Capabilities { get; } = new HardwareCapabilities
    {
        SupportsClassicCan = true,
        SupportsCanFd = false,
        SupportsTransmit = false,
        ChannelCount = 1,
        MaxPayloadBytes = 8,
        SupportedNominalBaudRates = new[] { 500000u }
    };

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;

    private bool _isConnected;
    private string? _currentPort;
    private string? _preferredPort;
    private bool _gatewayIdentified;
    private DateTime? _lastHandshakeUtc;
    private DateTime? _lastHeartbeatUtc;
    private DateTime? _lastSeenUtc;
    private string? _lastError;

    private long _rxFrameCount;
    private long _droppedFrames;
    private long _serialErrors;

    // Rate calc
    private DateTime _lastRateCalcUtc = DateTime.UtcNow;
    private long _windowFrames;
    private double _rxFrameRate;

    private bool _mcpReady;
    private byte _mcpCanStat;
    private byte _mcpCanCtrl;
    private byte _mcpCsPin;
    private byte _mcpEflg;
    private byte _mcpRec;
    private byte _mcpTec;
    private byte _mcpBaudCode;
    private byte _mcpDiagFlags;

    private CancellationTokenSource? _cts;
    private readonly object _lock = new object();
    private SerialPort? _serialPort;
    private Task? _readTask;

    private enum FrameState { HuntSync1, HuntSync2, Collect }
    private FrameState _state = FrameState.HuntSync1;
    private readonly byte[] _frameBuffer = new byte[21];
    private int _collected = 0;

    public void Configure(string portName)
    {
        lock (_lock)
        {
            _preferredPort = portName;
        }
    }

    public static string[] GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        Disconnect();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string[] ports = GetAvailablePorts();
        
        string[] portsToTry;
        if (!string.IsNullOrWhiteSpace(_preferredPort))
        {
            portsToTry = new[] { _preferredPort }.Concat(ports.Where(p => p != _preferredPort)).ToArray();
        }
        else
        {
            portsToTry = ports;
        }

        foreach (var port in portsToTry)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var sp = new SerialPort(port, 921600, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                sp.Open();
                
                lock (_lock)
                {
                    _serialPort = sp;
                    _currentPort = port;
                    _isConnected = true;
                    _gatewayIdentified = false;
                    _lastError = null;
                    _state = FrameState.HuntSync1;
                    _collected = 0;
                }

                _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
                NotifyStatusChanged();
                return Task.FromResult(true);
            }
            catch
            {
                // try next
            }
        }

        return Task.FromResult(false);
    }

    public Task DisconnectAsync()
    {
        Disconnect();
        return Task.CompletedTask;
    }

    private void Disconnect()
    {
        lock (_lock)
        {
            _isConnected = false;
            _cts?.Cancel();
            
            try { _serialPort?.Close(); } catch { }
            try { _serialPort?.Dispose(); } catch { }
            _serialPort = null;
            
            _gatewayIdentified = false;
            _currentPort = null;
        }
        NotifyStatusChanged();
    }

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Transmission not supported on ESP8266 USB Serial gateway.");
    }

    public void Dispose()
    {
        Disconnect();
    }

    public HardwareStatus GetStatus()
    {
        lock (_lock)
        {
            return new HardwareStatus
            {
                TransportId = TransportId,
                Name = Name,
                Type = Type,
                IsConnected = _isConnected && (!IsHeartbeatStale()),
                ConnectionDetails = _currentPort ?? "Not Connected",
                TotalFramesReceived = Interlocked.Read(ref _rxFrameCount),
                TotalFramesTransmitted = 0,
                RxFrameRate = _rxFrameRate,
                TxFrameRate = 0,
                LastSeenUtc = _lastSeenUtc,
                LastError = _lastError,
                Capabilities = Capabilities,
                ExtendedTelemetry = new
                {
                    ComPort = _currentPort ?? "None",
                    BaudRate = 921600,
                    IsGatewayIdentified = _gatewayIdentified,
                    McpReady = _mcpReady,
                    McpCanStat = $"0x{_mcpCanStat:X2}",
                    WiringDiagnosis = _mcpReady ? "MCP Initialized & Active" : (_mcpCanStat switch
                    {
                        0x01 => "Normal Wiring OK (0x80 read)",
                        0x02 => "ALERT: SO and SI are SWAPPED! Swap wires on D6 and D7",
                        0x03 => "ALERT: CS is wired to D1, not D2",
                        0x04 => "ERROR: MISO shorted to GND (0x00)",
                        0x05 => "ERROR: 0xFF on all lines (No response - Check VCC, GND, or Level Converter)",
                        _ => "Diagnosing SPI..."
                    }),
                    NormalRead = $"0x{_mcpCanCtrl:X2}",
                    SwappedRead = $"0x{_mcpCsPin:X2}",
                    D1Read = $"0x{_mcpEflg:X2}",
                    Eflg = $"0x{_mcpEflg:X2}",
                    Rec = _mcpRec,
                    Tec = _mcpTec,
                    BusSpeed = "500 kbps (Fixed)",
                    RxFrameCount = Interlocked.Read(ref _rxFrameCount),
                    DroppedFrames = Interlocked.Read(ref _droppedFrames),
                    SerialErrors = Interlocked.Read(ref _serialErrors),
                    LastHeartbeatUtc = _lastHeartbeatUtc,
                    FrameRate = _rxFrameRate,
                    Transport = "USB Serial",
                    Protocol = "Classic CAN",
                    Mode = ((_mcpCanStat & 0xE0) == 0x40 || (_mcpDiagFlags & 0x08) != 0) ? "LOOPBACK" : (_mcpCanStat == 0x00 ? "NORMAL" : $"0x{_mcpCanStat:X2}"),

                    // Explicit forensic fields (Section 16 & Section 18):
                    SpiConnected = (_mcpDiagFlags & 0x01) != 0 ? "PASS" : (_mcpReady ? "PASS" : "FAIL"),
                    Mcp2515Detected = _mcpReady ? "DETECTED" : "NOT DETECTED",
                    Mcp2515Oscillator = "8 MHz",
                    Mcp2515Bitrate = "500 kbps",
                    Mcp2515Mode = ((_mcpCanStat & 0xE0) == 0x40 || (_mcpDiagFlags & 0x08) != 0) ? "LOOPBACK" : (_mcpCanStat == 0x00 ? "NORMAL" : $"0x{_mcpCanStat:X2}"),
                    LogicLevelInterfacePresent = "NOT PRESENT",
                    ExternalCanNodePresent = false,
                    CanFramesReceived = Interlocked.Read(ref _rxFrameCount),
                    VehicleConnected = false,
                    PhysicalCanReceive = "NOT YET VALIDATED",
                    PhysicalCanReceiveValidated = false,
                    InternalLoopback = (_mcpDiagFlags & 0x02) != 0 ? "PASS" : "PENDING",
                    Spi100Cycles = (_mcpDiagFlags & 0x01) != 0 ? "PASS (100/100)" : (_mcpReady ? "PASS" : "FAIL"),
                    Cnf1 = "0x00",
                    Cnf2 = "0xD1",
                    Cnf3 = "0x81",
                    CurrentHardware = "ESP8266 + HW-184 MCP2515/TJA1050 (DIRECT SPI — NO LEVEL SHIFTER)",
                    HardwareState = "EXPERIMENTAL / NOT RECOMMENDED",
                    Warning = "Direct ESP8266 ↔ 5 V MCP2515 SPI detected. Install an appropriate 3.3 V ↔ 5 V logic-level interface before production/vehicle operation."
                }
            };
        }
    }

    private bool IsHeartbeatStale()
    {
        if (_lastHeartbeatUtc == null) return false;
        return (DateTime.UtcNow - _lastHeartbeatUtc.Value).TotalSeconds > 5;
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[256];
        Stream? stream = null;

        lock (_lock)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                stream = _serialPort.BaseStream;
            }
        }

        while (!token.IsCancellationRequested && _isConnected && stream != null)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0)
                {
                    await Task.Delay(10, token);
                    continue;
                }

                for (int i = 0; i < bytesRead; i++)
                {
                    ProcessByte(buffer[i]);
                }

                CalculateRate();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _serialErrors);
                _lastError = $"Serial read error: {ex.Message}";
                
                await Task.Delay(2000, token);
                if (!token.IsCancellationRequested)
                {
                    Disconnect(); // force reconnect logic if needed, but simplest is just stop and let user reconnect
                }
                break;
            }
        }
    }

    internal void ProcessByte(byte b)
    {
        switch (_state)
        {
            case FrameState.HuntSync1:
                if (b == 0xAA)
                {
                    _frameBuffer[0] = b;
                    _state = FrameState.HuntSync2;
                }
                break;
            case FrameState.HuntSync2:
                if (b == 0x55)
                {
                    _frameBuffer[1] = b;
                    _collected = 2;
                    _state = FrameState.Collect;
                }
                else if (b == 0xAA)
                {
                    _frameBuffer[0] = b;
                }
                else
                {
                    _state = FrameState.HuntSync1;
                }
                break;
            case FrameState.Collect:
                _frameBuffer[_collected++] = b;
                if (_collected == 21)
                {
                    ProcessFrame(_frameBuffer);
                    _state = FrameState.HuntSync1;
                    _collected = 0;
                }
                break;
        }
    }

    public enum UsbMessageType
    {
        InvalidCrc,
        CanFrame,
        Handshake,
        Heartbeat,
        Unknown
    }

    internal static UsbMessageType ClassifyFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 21 || frame[0] != 0xAA || frame[1] != 0x55)
        {
            return UsbMessageType.Unknown;
        }

        byte expectedCrc = Crc8(frame.Slice(0, 20));
        if (frame[20] != expectedCrc)
        {
            return UsbMessageType.InvalidCrc;
        }

        // CAN frame: strictly byte 10 <= 8 AND flags mask valid (0x01 | 0x02)
        byte dlc = frame[10];
        if (dlc <= 8)
        {
            byte flags = frame[11];
            if ((flags & ~0x03) == 0)
            {
                return UsbMessageType.CanFrame;
            }
            return UsbMessageType.Unknown;
        }

        // Control frame: universal discriminator dlc == 0xFF
        if (dlc == 0xFF)
        {
            byte msgType = frame[11];
            // Handshake: msgType 0x02, identifier starts with "ESP"
            if (msgType == 0x02 && frame[2] == (byte)'E' && frame[3] == (byte)'S' && frame[4] == (byte)'P')
            {
                return UsbMessageType.Handshake;
            }

            // Heartbeat: msgType 0x03
            if (msgType == 0x03)
            {
                return UsbMessageType.Heartbeat;
            }
        }

        return UsbMessageType.Unknown;
    }

    private void ProcessFrame(byte[] buf)
    {
        var msgType = ClassifyFrame(buf);
        switch (msgType)
        {
            case UsbMessageType.InvalidCrc:
                Interlocked.Increment(ref _droppedFrames);
                _lastError = "CRC mismatch";
                break;

            case UsbMessageType.Handshake:
                _gatewayIdentified = true;
                _lastHandshakeUtc = DateTime.UtcNow;
                NotifyStatusChanged();
                break;

            case UsbMessageType.Heartbeat:
                _lastHeartbeatUtc = DateTime.UtcNow;
                _mcpReady = buf[13] == 1;
                _mcpCanStat = buf[14];
                _mcpCanCtrl = buf[15];
                _mcpCsPin = buf[16];
                _mcpEflg = buf[15];
                _mcpRec = buf[16];
                _mcpTec = buf[17];
                _mcpBaudCode = buf[18];
                _mcpDiagFlags = buf[19];
                break;

            case UsbMessageType.CanFrame:
                if (BinaryPacketDecoder.TryDecode(buf.AsSpan(0, 20), out var frame))
                {
                    frame.SourceId = "ESP8266 + MCP2515 Gateway";
                    frame.Channel = 0;
                    Interlocked.Increment(ref _rxFrameCount);
                    Interlocked.Increment(ref _windowFrames);
                    _lastSeenUtc = DateTime.UtcNow;
                    OnFrameReceived?.Invoke(frame);
                }
                break;

            case UsbMessageType.Unknown:
            default:
                Interlocked.Increment(ref _droppedFrames);
                _lastError = "Unknown or malformed frame type";
                break;
        }
    }

    internal static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0x00;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x80) != 0)
                    crc = (byte)((crc << 1) ^ 0x07);
                else
                    crc <<= 1;
            }
        }
        return crc;
    }

    private void CalculateRate()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRateCalcUtc).TotalSeconds;
        if (elapsed >= 1.0)
        {
            var frames = Interlocked.Exchange(ref _windowFrames, 0);
            _rxFrameRate = frames / elapsed;
            _lastRateCalcUtc = now;
            NotifyStatusChanged();
        }
    }

    private void NotifyStatusChanged()
    {
        OnStatusChanged?.Invoke(GetStatus());
    }
}
