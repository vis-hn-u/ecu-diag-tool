using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware.Transports;

public class GvretSerialTransport : ICanTransport
{
    public const string TransportIdentifier = "arduino-nano-gvret";
    public const int DefaultUartBaud = 115200;
    public const uint DefaultCanBusSpeed = 500000u;

    // GVRET Protocol Command Constants (from SavvyCAN connections/gvretserial.h)
    public const byte ProtoBuildCanFrame = 0x00;
    public const byte ProtoTimeSync = 0x01;
    public const byte ProtoDigInputs = 0x02;
    public const byte ProtoAnaInputs = 0x03;
    public const byte ProtoSetDigOut = 0x04;
    public const byte ProtoSetupCanbus = 0x05;
    public const byte ProtoGetCanbusParams = 0x06;
    public const byte ProtoGetDevInfo = 0x07;
    public const byte ProtoSetSwMode = 0x08;
    public const byte ProtoKeepalive = 0x09;
    public const byte ProtoSetSystype = 0x0A;
    public const byte ProtoEcho = 0x0B;
    public const byte ProtoGetNumBuses = 0x0C;
    public const byte ProtoGetExtBuses = 0x0D;
    public const byte ProtoSetExtBuses = 0x0E;

    public string TransportId => TransportIdentifier;
    public string Name => "Arduino Nano + MCP2515 (GVRET / SavvyCAN)";
    public TransportType Type => TransportType.GvretSerial;

    public HardwareCapabilities Capabilities { get; } = new HardwareCapabilities
    {
        SupportsClassicCan = true,
        SupportsCanFd = false,
        SupportsTransmit = true,
        ChannelCount = 1,
        MaxPayloadBytes = 8,
        SupportedNominalBaudRates = new[] { 125000u, 250000u, 500000u, 1000000u }
    };

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;

    private bool _isConnected;
    private string? _currentPort;
    private string? _preferredPort;
    private int _uartBaud = DefaultUartBaud;
    private uint _busSpeed = DefaultCanBusSpeed;
    private bool _listenOnly = true; // Hardware-level safety default: listen-only armed

    private bool _deviceIdentified;
    private bool _keepaliveValidated;
    private ushort _firmwareBuild;
    private byte _numBuses = 1;
    private DateTime? _lastHandshakeUtc;
    private DateTime? _lastHeartbeatUtc;
    private DateTime? _lastSeenUtc;
    private string? _lastError;

    private long _rxFrameCount;
    private long _txFrameCount;
    private long _droppedFrames;
    private long _serialErrors;

    private DateTime _lastRateCalcUtc = DateTime.UtcNow;
    private long _windowFrames;
    private double _rxFrameRate;

    private CancellationTokenSource? _cts;
    private readonly object _lock = new object();
    private SerialPort? _serialPort;
    private Task? _readTask;

    // Byte-level protocol parser state machine
    internal enum RxState
    {
        Idle,
        GetCommand,
        BuildCanFrame,
        TimeSync,
        GetCanbusParams,
        GetDeviceInfo,
        Keepalive,
        GetNumBuses,
        GetExtBuses
    }

    internal RxState _rxState = RxState.Idle;
    internal int _rxStep = 0;
    internal uint _buildTimestamp = 0;
    internal uint _buildId = 0;
    internal bool _buildExtended = false;
    internal byte _buildBus = 0;
    internal byte _buildDlc = 0;
    internal byte[] _buildData = new byte[8];

    public bool IsConnected
    {
        get
        {
            lock (_lock) return _isConnected;
        }
    }

    public string? ConnectionDetails
    {
        get
        {
            lock (_lock) return _currentPort ?? "Not Connected";
        }
    }

    public bool IsListenOnly
    {
        get
        {
            lock (_lock) return _listenOnly;
        }
    }

    public uint BusSpeed
    {
        get
        {
            lock (_lock) return _busSpeed;
        }
    }

    public void Configure(string portName, uint busSpeed = DefaultCanBusSpeed, bool listenOnly = true, int baudRate = DefaultUartBaud)
    {
        lock (_lock)
        {
            _preferredPort = portName;
            _busSpeed = busSpeed;
            _listenOnly = listenOnly;
            _uartBaud = baudRate;
        }

        // If actively connected, immediately update hardware-level listen-only and bitrate
        if (IsConnected)
        {
            ApplyHardwareBusConfig();
        }
    }

    public void SetListenOnly(bool listenOnly)
    {
        lock (_lock)
        {
            _listenOnly = listenOnly;
        }

        if (IsConnected)
        {
            ApplyHardwareBusConfig();
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
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_preferredPort) && ports.Contains(_preferredPort, StringComparer.OrdinalIgnoreCase))
            {
                portsToTry = new[] { _preferredPort };
            }
            else
            {
                // Prioritize COM8 if present, else try all available
                var sorted = ports.OrderByDescending(p => p.Equals("COM8", StringComparison.OrdinalIgnoreCase)).ToArray();
                portsToTry = sorted;
            }
        }

        if (portsToTry.Length == 0)
        {
            _lastError = "No serial COM ports found.";
            return Task.FromResult(false);
        }

        foreach (var port in portsToTry)
        {
            try
            {
                var sp = new SerialPort(port, _uartBaud, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    DtrEnable = false,
                    RtsEnable = false
                };

                sp.Open();
                sp.DiscardInBuffer();
                sp.DiscardOutBuffer();

                _serialPort = sp;
                _currentPort = port;
                _isConnected = true;
                _lastError = null;

                // Send the exact SavvyCAN handshake sequence to initialize GVRET binary mode
                SendSavvyCanHandshake();

                // Apply hardware-level CAN bus bitrate and listen-only mode
                ApplyHardwareBusConfig();

                _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
                NotifyStatusChanged();
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _lastError = $"Port {port}: {ex.Message}";
                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        _isConnected = false;
        NotifyStatusChanged();
        return Task.FromResult(false);
    }

    public Task DisconnectAsync()
    {
        Disconnect();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Disconnect();
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            if (!_isConnected && _serialPort == null) return;

            try { _cts?.Cancel(); } catch { }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
                catch { }
                finally
                {
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }

            _isConnected = false;
            _currentPort = null;
            _deviceIdentified = false;
            _keepaliveValidated = false;
            _rxState = RxState.Idle;
            _rxStep = 0;
        }

        NotifyStatusChanged();
    }

    public void Dispose()
    {
        Disconnect();
    }

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                throw new InvalidOperationException("GVRET Transport is not connected.");
            }

            // HARDWARE SAFETY INTERLOCK: Reject transmission when in Listen-Only mode
            if (_listenOnly)
            {
                throw new InvalidOperationException("Transmission rejected: Hardware is armed in LISTEN-ONLY mode.");
            }

            byte[] packet = BuildOutboundFrame(frame, channel: 0);
            try
            {
                _serialPort.Write(packet, 0, packet.Length);
                Interlocked.Increment(ref _txFrameCount);
                return ValueTask.FromResult(true);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _serialErrors);
                _lastError = $"TX Error: {ex.Message}";
                throw;
            }
        }
    }

    public HardwareStatus GetStatus()
    {
        long rx;
        long tx;
        long dropped;
        long errs;
        double rate;
        string? curPort;
        bool conn;
        bool listen;
        uint speed;
        bool devId;
        bool keepalive;
        ushort build;
        byte buses;
        DateTime? lastHb;
        DateTime? lastSeen;
        string? lastErr;

        lock (_lock)
        {
            rx = Interlocked.Read(ref _rxFrameCount);
            tx = Interlocked.Read(ref _txFrameCount);
            dropped = Interlocked.Read(ref _droppedFrames);
            errs = Interlocked.Read(ref _serialErrors);
            rate = _rxFrameRate;
            curPort = _currentPort;
            conn = _isConnected;
            listen = _listenOnly;
            speed = _busSpeed;
            devId = _deviceIdentified;
            keepalive = _keepaliveValidated;
            build = _firmwareBuild;
            buses = _numBuses;
            lastHb = _lastHeartbeatUtc;
            lastSeen = _lastSeenUtc;
            lastErr = _lastError;
        }

        return new HardwareStatus
        {
            TransportId = TransportId,
            Name = Name,
            Type = Type,
            IsConnected = conn,
            ConnectionDetails = curPort ?? "Not Connected",
            Capabilities = Capabilities,
            TotalFramesReceived = rx,
            TotalFramesTransmitted = tx,
            RxFrameRate = rate,
            TxFrameRate = 0,
            LastSeenUtc = lastSeen,
            LastError = lastErr,
            ExtendedTelemetry = new
            {
                ComPort = curPort ?? "None",
                BaudRate = _uartBaud,
                BusSpeed = $"{speed / 1000} kbps",
                ListenOnly = listen,
                OperatingMode = listen ? "LISTEN ONLY (SILENT)" : "NORMAL (TRANSMIT ENABLED)",
                IsDeviceIdentified = devId,
                IsKeepaliveValidated = keepalive,
                FirmwareBuild = build > 0 ? build.ToString() : "Unknown",
                BusCount = buses,
                RxFrameCount = rx,
                TxFrameCount = tx,
                DroppedFrames = dropped,
                SerialErrors = errs,
                LastHeartbeatUtc = lastHb,
                LastSeenUtc = lastSeen,
                Transport = "Arduino Nano GVRET Serial",
                HardwareOscillator = "8 MHz MCP2515",
                Architecture = "Single Shared Transport (SavvyCAN + ECU Diagnostics)"
            }
        };
    }

    /// <summary>
    /// Exact pinned SavvyCAN handshake sequence matching connections/gvretserial.cpp lines 374..410:
    ///   0xE7, 0xE7 -> Enter binary GVRET mode
    ///   0xF1 0x0C  -> Query number of buses (PROTO_GET_NUMBUSES)
    ///   0xF1 0x06  -> Query CAN bus parameters (PROTO_GET_CANBUS_PARAMS)
    ///   0xF1 0x07  -> Query device info (PROTO_GET_DEV_INFO)
    ///   0xF1 0x01  -> Time sync request (PROTO_TIME_SYNC)
    ///   0xF1 0x09  -> Keepalive / comm validation (PROTO_KEEPALIVE)
    /// </summary>
    internal static byte[] BuildHandshakePacket()
    {
        return new byte[]
        {
            0xE7, 0xE7,
            0xF1, ProtoGetNumBuses,
            0xF1, ProtoGetCanbusParams,
            0xF1, ProtoGetDevInfo,
            0xF1, ProtoTimeSync,
            0xF1, ProtoKeepalive
        };
    }

    /// <summary>
    /// Builds CAN bus configuration command matching connections/gvretserial.cpp lines 189..205
    /// and nano_mcp2515_savvycan/gvret_protocol.cpp lines 205..231:
    ///   F1 05 <can0_raw: 4B LE> <can1_raw: 4B LE> 00
    /// Bit 31 (0x80000000): Configuration valid
    /// Bit 30 (0x40000000): Bus enabled
    /// Bit 29 (0x20000000): Hardware Listen-Only mode (1 = Listen Only, 0 = Normal / Tx)
    /// Bits 0..27: Bitrate in bps (e.g. 500000)
    /// </summary>
    internal static byte[] BuildBusSetupCommand(uint baudRate, bool listenOnly)
    {
        uint can0Raw = baudRate & 0x0FFFFFFFu;
        can0Raw |= 0x80000000u; // Enabled / valid bit
        can0Raw |= 0x40000000u; // Bus active bit

        if (listenOnly)
        {
            can0Raw |= 0x20000000u; // Hardware Listen-Only bit
        }

        uint can1Raw = 0u; // Second bus disabled

        byte[] cmd = new byte[11];
        cmd[0] = 0xF1;
        cmd[1] = ProtoSetupCanbus; // 0x05

        cmd[2] = (byte)(can0Raw & 0xFF);
        cmd[3] = (byte)((can0Raw >> 8) & 0xFF);
        cmd[4] = (byte)((can0Raw >> 16) & 0xFF);
        cmd[5] = (byte)((can0Raw >> 24) & 0xFF);

        cmd[6] = (byte)(can1Raw & 0xFF);
        cmd[7] = (byte)((can1Raw >> 8) & 0xFF);
        cmd[8] = (byte)((can1Raw >> 16) & 0xFF);
        cmd[9] = (byte)((can1Raw >> 24) & 0xFF);

        cmd[10] = 0x00; // Terminator
        return cmd;
    }

    /// <summary>
    /// Encodes an outbound CAN frame matching SavvyCAN connections/gvretserial.cpp lines 263..285:
    ///   F1 00 <ID: 4B LE (Bit 31=Ext)> <Bus: 1B> <DLC: 1B> <Data: 0..8B> 00
    /// </summary>
    internal static byte[] BuildOutboundFrame(CanFrame frame, byte channel = 0)
    {
        uint encodedId = frame.Id;
        if (frame.IsExtended)
        {
            encodedId |= 0x80000000u; // Bit 31 marks extended frame
        }

        byte dlc = (byte)Math.Min((int)frame.Dlc, 8);
        byte[] buffer = new byte[8 + dlc + 1];

        buffer[0] = 0xF1;
        buffer[1] = ProtoBuildCanFrame; // 0x00

        buffer[2] = (byte)(encodedId & 0xFF);
        buffer[3] = (byte)((encodedId >> 8) & 0xFF);
        buffer[4] = (byte)((encodedId >> 16) & 0xFF);
        buffer[5] = (byte)((encodedId >> 24) & 0xFF);

        buffer[6] = (byte)(channel & 0x03);
        buffer[7] = dlc;

        if (dlc > 0 && frame.Data != null)
        {
            Array.Copy(frame.Data, 0, buffer, 8, Math.Min(frame.Data.Length, (int)dlc));
        }

        buffer[buffer.Length - 1] = 0x00; // Trailing terminator
        return buffer;
    }

    private void SendSavvyCanHandshake()
    {
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                byte[] handshake = BuildHandshakePacket();
                _serialPort.Write(handshake, 0, handshake.Length);
                _lastHandshakeUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _serialErrors);
            _lastError = $"Handshake error: {ex.Message}";
        }
    }

    private void ApplyHardwareBusConfig()
    {
        try
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                byte[] cmd = BuildBusSetupCommand(_busSpeed, _listenOnly);
                _serialPort.Write(cmd, 0, cmd.Length);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _serialErrors);
            _lastError = $"Bus config error: {ex.Message}";
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        byte[] readBuffer = new byte[512];
        Stream? stream = null;

        try
        {
            stream = _serialPort?.BaseStream;
        }
        catch { }

        if (stream == null) return;

        while (!token.IsCancellationRequested)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, token);
                if (bytesRead == 0)
                {
                    await Task.Delay(10, token);
                    continue;
                }

                for (int i = 0; i < bytesRead; i++)
                {
                    ProcessByte(readBuffer[i]);
                }

                CalculateRate(bytesRead);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                Interlocked.Increment(ref _serialErrors);
                _lastError = $"IO Error: {ex.Message}";
                break;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _serialErrors);
                _lastError = $"Unexpected read error: {ex.Message}";
                await Task.Delay(100, token);
            }
        }

        lock (_lock)
        {
            _isConnected = false;
        }
        NotifyStatusChanged();
    }

    /// <summary>
    /// Byte-level state machine parser for inbound GVRET bytes.
    /// Matches SavvyCAN connections/gvretserial.cpp procRXChar() logic.
    /// </summary>
    internal void ProcessByte(byte b)
    {
        switch (_rxState)
        {
            case RxState.Idle:
                if (b == 0xF1)
                {
                    _rxState = RxState.GetCommand;
                }
                break;

            case RxState.GetCommand:
                switch (b)
                {
                    case ProtoBuildCanFrame: // 0x00: Incoming CAN Frame
                        _rxState = RxState.BuildCanFrame;
                        _rxStep = 0;
                        _buildTimestamp = 0;
                        _buildId = 0;
                        _buildExtended = false;
                        _buildDlc = 0;
                        _buildBus = 0;
                        break;

                    case ProtoTimeSync: // 0x01
                        _rxState = RxState.TimeSync;
                        _rxStep = 0;
                        break;

                    case ProtoGetCanbusParams: // 0x06
                        _rxState = RxState.GetCanbusParams;
                        _rxStep = 0;
                        break;

                    case ProtoGetDevInfo: // 0x07
                        _rxState = RxState.GetDeviceInfo;
                        _rxStep = 0;
                        break;

                    case ProtoKeepalive: // 0x09: Keepalive response (DE AD)
                        _rxState = RxState.Keepalive;
                        _rxStep = 0;
                        break;

                    case ProtoGetNumBuses: // 0x0C
                        _rxState = RxState.GetNumBuses;
                        _rxStep = 0;
                        break;

                    case ProtoGetExtBuses: // 0x0D
                        _rxState = RxState.GetExtBuses;
                        _rxStep = 0;
                        break;

                    default:
                        _rxState = RxState.Idle;
                        break;
                }
                break;

            case RxState.BuildCanFrame:
                switch (_rxStep)
                {
                    case 0: _buildTimestamp = (uint)b; break;
                    case 1: _buildTimestamp |= ((uint)b << 8); break;
                    case 2: _buildTimestamp |= ((uint)b << 16); break;
                    case 3: _buildTimestamp |= ((uint)b << 24); break;

                    case 4: _buildId = (uint)b; break;
                    case 5: _buildId |= ((uint)b << 8); break;
                    case 6: _buildId |= ((uint)b << 16); break;
                    case 7:
                        _buildId |= ((uint)b << 24);
                        if ((_buildId & 0x80000000u) != 0)
                        {
                            _buildExtended = true;
                            _buildId &= 0x7FFFFFFFu;
                        }
                        else
                        {
                            _buildExtended = false;
                        }
                        break;

                    case 8:
                        _buildDlc = (byte)(b & 0x0F);
                        if (_buildDlc > 8) _buildDlc = 8;
                        _buildBus = (byte)((b & 0xF0) >> 4);
                        break;

                    default:
                        int dataIdx = _rxStep - 9;
                        if (dataIdx < _buildDlc)
                        {
                            _buildData[dataIdx] = b;
                        }

                        if (_rxStep >= (_buildDlc + 8))
                        {
                            DispatchReceivedFrame();
                            _rxState = RxState.Idle;
                            _rxStep = 0;
                            return;
                        }
                        break;
                }
                _rxStep++;
                break;

            case RxState.Keepalive:
                _rxStep++;
                if (_rxStep >= 2)
                {
                    _keepaliveValidated = true;
                    _lastHeartbeatUtc = DateTime.UtcNow;
                    _rxState = RxState.Idle;
                    _rxStep = 0;
                }
                break;

            case RxState.GetDeviceInfo:
                if (_rxStep == 0) _firmwareBuild = (ushort)b;
                else if (_rxStep == 1) _firmwareBuild |= (ushort)(b << 8);

                _rxStep++;
                if (_rxStep >= 4)
                {
                    _deviceIdentified = true;
                    _rxState = RxState.Idle;
                    _rxStep = 0;
                }
                break;

            case RxState.GetNumBuses:
                _numBuses = b;
                _rxState = RxState.Idle;
                _rxStep = 0;
                break;

            case RxState.GetCanbusParams:
                _rxStep++;
                if (_rxStep >= 8)
                {
                    _rxState = RxState.Idle;
                    _rxStep = 0;
                }
                break;

            case RxState.TimeSync:
                _rxStep++;
                if (_rxStep >= 4)
                {
                    _rxState = RxState.Idle;
                    _rxStep = 0;
                }
                break;

            case RxState.GetExtBuses:
                _rxStep++;
                if (_rxStep >= 12)
                {
                    _rxState = RxState.Idle;
                    _rxStep = 0;
                }
                break;

            default:
                _rxState = RxState.Idle;
                _rxStep = 0;
                break;
        }
    }

    private void DispatchReceivedFrame()
    {
        byte flags = 0;
        if (_buildExtended)
        {
            flags |= CanFrame.FlagExtended;
        }

        byte[] frameData = new byte[8];
        if (_buildDlc > 0)
        {
            Array.Copy(_buildData, frameData, _buildDlc);
        }

        var frame = new CanFrame
        {
            TimestampUs = _buildTimestamp,
            Id = _buildId,
            Dlc = _buildDlc,
            Flags = flags,
            Data = frameData,
            Channel = _buildBus,
            SourceId = "Arduino Nano GVRET"
        };

        Interlocked.Increment(ref _rxFrameCount);
        _lastSeenUtc = DateTime.UtcNow;

        try
        {
            OnFrameReceived?.Invoke(frame);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _droppedFrames);
            _lastError = $"Subscriber exception: {ex.Message}";
        }
    }

    private void CalculateRate(int bytesRead)
    {
        Interlocked.Add(ref _windowFrames, 1);
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRateCalcUtc).TotalSeconds;
        if (elapsed >= 1.0)
        {
            long frames = Interlocked.Exchange(ref _windowFrames, 0);
            _rxFrameRate = Math.Round(frames / elapsed, 1);
            _lastRateCalcUtc = now;
        }
    }

    private void NotifyStatusChanged()
    {
        try
        {
            OnStatusChanged?.Invoke(GetStatus());
        }
        catch { }
    }
}
