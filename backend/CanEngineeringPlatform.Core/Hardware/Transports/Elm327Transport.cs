using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Hardware.Transports.Elm327;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware.Transports;

/// <summary>
/// Hardware transport implementation for ELM327 Bluetooth (RFCOMM/SPP) and serial OBD-II adapters.
/// Implements ICanTransport and IAutomotiveTransport within the unified hardware abstraction.
/// Converts ELM327 responses into canonical CanFrame objects for seamless downstream processing
/// (OBD, UDS, ISO-TP, live data, telemetry, and diagnostics) without creating a second pipeline.
/// </summary>
public class Elm327Transport : ICanTransport, IAutomotiveTransport, IDisposable
{
    public string TransportId => "elm327-adapter";
    public string Name => $"ELM327 Bluetooth ({AdapterInfo.DeviceName})";
    public TransportType Type => TransportType.Elm327;

    public AutomotiveProtocol Protocol => AdapterInfo.ActiveProtocol.ToAutomotiveProtocol();
    public TransportState State => ConnectionState switch
    {
        Elm327ConnectionState.Connected => TransportState.Connected,
        Elm327ConnectionState.Connecting or Elm327ConnectionState.Handshaking => TransportState.Connecting,
        Elm327ConnectionState.Reconnecting => TransportState.Connecting,
        Elm327ConnectionState.Error => TransportState.Error,
        _ => TransportState.Disconnected
    };

    public HardwareCapabilities Capabilities { get; } = new()
    {
        SupportsCanFd = false,
        SupportsBitRateSwitch = false,
        SupportsClassicCan = true,
        ChannelCount = 1,
        MaxPayloadBytes = 8,
        SupportsTransmit = true,
        SupportsHardwareTimestamps = false,
        SupportedNominalBaudRates = new uint[] { 250000, 500000 },
        SupportedDataBaudRates = Array.Empty<uint>()
    };

    private readonly object _lock = new();
    private IElm327CommunicationChannel _channel;
    private readonly bool _ownsChannel;

    private bool _isConnected;
    private long _totalFramesReceived;
    private long _totalFramesTransmitted;
    private double _rxFrameRate;
    private double _txFrameRate;
    private DateTime? _lastSeenUtc;
    private string? _lastError;
    private DateTime _lastFpsCalc = DateTime.UtcNow;
    private int _windowFramesReceived;
    private int _windowFramesTransmitted;

    // Configuration & State
    public string PortName { get; private set; } = "Loopback";
    public int SerialBaudRate { get; private set; } = 115200;
    public uint NominalBaudRate { get; private set; } = 500000;
    public string ActiveProtocolCode { get; private set; } = "6"; // 6 = ISO 15765-4 (CAN 11/500)
    public bool AutoReconnect { get; set; } = true;
    public Elm327ConnectionState ConnectionState { get; private set; } = Elm327ConnectionState.Disconnected;
    public Elm327AdapterInfo AdapterInfo { get; } = new();
    public Elm327Capabilities ElmCapabilities { get; } = new();

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;
    public event Action<AutomotiveTransportStatus>? OnAutomotiveStatusChanged;

    public Elm327Transport(string portName = "Loopback", int baudRate = 115200)
    {
        PortName = string.IsNullOrWhiteSpace(portName) ? "Loopback" : portName.Trim();
        SerialBaudRate = baudRate > 0 ? baudRate : 115200;
        _channel = CreateChannel(PortName, SerialBaudRate);
        _ownsChannel = true;
        UpdateAdapterInfo();
    }

    /// <summary>
    /// Constructor for dependency injection of mock/simulated channels during testing.
    /// </summary>
    public Elm327Transport(IElm327CommunicationChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _ownsChannel = false;
        PortName = channel.ChannelName;
        UpdateAdapterInfo();
    }

    private static IElm327CommunicationChannel CreateChannel(string portName, int baudRate)
    {
        if (portName.Equals("Loopback", StringComparison.OrdinalIgnoreCase) ||
            portName.StartsWith("Simulated", StringComparison.OrdinalIgnoreCase) ||
            portName.StartsWith("SIM_", StringComparison.OrdinalIgnoreCase))
        {
            return new SimulatedElmChannel();
        }
        return new SerialPortElmChannel(portName, baudRate);
    }

    public void Configure(string? portName = null, int? serialBaudRate = null, uint? nominalBaudRate = null, Elm327Protocol? protocol = null)
    {
        lock (_lock)
        {
            bool needRecreate = false;
            if (!string.IsNullOrWhiteSpace(portName) && !portName.Equals(PortName, StringComparison.OrdinalIgnoreCase))
            {
                PortName = portName.Trim();
                needRecreate = true;
            }
            if (serialBaudRate.HasValue && serialBaudRate.Value > 0 && serialBaudRate.Value != SerialBaudRate)
            {
                SerialBaudRate = serialBaudRate.Value;
                needRecreate = true;
            }
            if (nominalBaudRate.HasValue && nominalBaudRate.Value > 0)
            {
                NominalBaudRate = nominalBaudRate.Value;
                if (!protocol.HasValue)
                {
                    ActiveProtocolCode = (NominalBaudRate == 250000) ? "8" : "6";
                }
            }
            if (protocol.HasValue)
            {
                ActiveProtocolCode = protocol.Value.ToProtocolCode();
            }

            if (needRecreate && _ownsChannel)
            {
                _channel.Dispose();
                _channel = CreateChannel(PortName, SerialBaudRate);
            }
            UpdateAdapterInfo();
        }
        NotifyStatusChanged();
    }

    private void UpdateAdapterInfo()
    {
        AdapterInfo.PortName = PortName;
        AdapterInfo.BaudRate = SerialBaudRate;
        AdapterInfo.ActiveProtocol = Elm327ProtocolExtensions.FromProtocolCode(ActiveProtocolCode);
        AdapterInfo.ActiveProtocolName = AdapterInfo.ActiveProtocol.GetDescription();
        AdapterInfo.ConnectionType = PortName.Equals("Loopback", StringComparison.OrdinalIgnoreCase) || PortName.Contains("Simulated")
            ? "Virtual Loopback (Simulated)"
            : "Bluetooth RFCOMM / Serial COM";
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            ConnectionState = Elm327ConnectionState.Connecting;
            _lastError = null;
        }
        NotifyStatusChanged();

        try
        {
            bool channelOpened = await _channel.OpenAsync(cancellationToken);
            if (!channelOpened)
            {
                SetError("Failed to open communication channel to ELM327 device.");
                return false;
            }

            lock (_lock)
            {
                ConnectionState = Elm327ConnectionState.Handshaking;
            }
            NotifyStatusChanged();

            // Execute standard ELM327 initialization and identification handshake
            bool handshakeOk = await PerformHandshakeAsync(cancellationToken);
            if (!handshakeOk)
            {
                SetError("ELM327 initialization handshake failed.");
                await _channel.CloseAsync();
                return false;
            }

            lock (_lock)
            {
                _isConnected = true;
                ConnectionState = Elm327ConnectionState.Connected;
                AdapterInfo.ConnectedAtUtc = DateTime.UtcNow;
                _lastSeenUtc = DateTime.UtcNow;
                _lastError = null;
            }
            NotifyStatusChanged();
            return true;
        }
        catch (Exception ex)
        {
            SetError($"ELM327 connection error: {ex.Message}");
            await _channel.CloseAsync();
            return false;
        }
    }

    private async Task<bool> PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        // 1. Reset chip: ATZ
        var resetResp = await SendCommandInternalAsync("ATZ", TimeSpan.FromSeconds(3), cancellationToken);
        if (resetResp.Success && !string.IsNullOrWhiteSpace(resetResp.RawResponse))
        {
            string ver = ExtractElmVersion(resetResp.RawResponse);
            if (!string.IsNullOrEmpty(ver))
            {
                AdapterInfo.FirmwareVersion = ver;
                AdapterInfo.DeviceName = ver;
            }
        }

        // 2. Echo Off: ATE0
        await SendCommandInternalAsync("ATE0", TimeSpan.FromSeconds(1), cancellationToken);

        // 3. Linefeeds Off: ATL0
        await SendCommandInternalAsync("ATL0", TimeSpan.FromSeconds(1), cancellationToken);

        // 4. Headers On: ATH1 (essential to extract CAN IDs)
        var headersResp = await SendCommandInternalAsync("ATH1", TimeSpan.FromSeconds(1), cancellationToken);
        ElmCapabilities.SupportsCustomHeaders = headersResp.Success;

        // 5. Spaces On: ATS1
        await SendCommandInternalAsync("ATS1", TimeSpan.FromSeconds(1), cancellationToken);

        // 6. Adaptive Timing: ATAT1
        var atResp = await SendCommandInternalAsync("ATAT1", TimeSpan.FromSeconds(1), cancellationToken);
        ElmCapabilities.SupportsAdaptiveTiming = atResp.Success;

        // 7. Adapter identification: ATI
        var identResp = await SendCommandInternalAsync("ATI", TimeSpan.FromSeconds(1), cancellationToken);
        if (identResp.Success && identResp.Lines.Count > 0)
        {
            AdapterInfo.FirmwareVersion = identResp.Lines[0].Trim();
            AdapterInfo.DeviceName = AdapterInfo.FirmwareVersion;
        }

        // 8. Device description: AT@1
        var descResp = await SendCommandInternalAsync("AT@1", TimeSpan.FromSeconds(1), cancellationToken);
        if (descResp.Success && descResp.Lines.Count > 0)
        {
            AdapterInfo.Description = descResp.Lines[0].Trim();
        }

        // 9. Battery Voltage: ATRV
        var voltResp = await SendCommandInternalAsync("ATRV", TimeSpan.FromSeconds(1), cancellationToken);
        if (voltResp.Success && voltResp.Lines.Count > 0)
        {
            AdapterInfo.BatteryVoltage = ParseVoltage(voltResp.Lines[0]);
        }

        // 10. Configure protocol: ATSP<ActiveProtocolCode> or ATSP0
        string protoCmd = $"ATSP{ActiveProtocolCode}";
        var protoResp = await SendCommandInternalAsync(protoCmd, TimeSpan.FromSeconds(2), cancellationToken);
        if (!protoResp.Success)
        {
            // Fallback to auto-protocol ATSP0
            await SendCommandInternalAsync("ATSP0", TimeSpan.FromSeconds(2), cancellationToken);
        }

        // 11. Read active protocol description: ATDP & ATDPN
        var dpnResp = await SendCommandInternalAsync("ATDPN", TimeSpan.FromSeconds(1), cancellationToken);
        if (dpnResp.Success && dpnResp.Lines.Count > 0)
        {
            AdapterInfo.ActiveProtocol = Elm327ProtocolExtensions.FromProtocolCode(dpnResp.Lines[0]);
        }

        var dpResp = await SendCommandInternalAsync("ATDP", TimeSpan.FromSeconds(1), cancellationToken);
        if (dpResp.Success && dpResp.Lines.Count > 0)
        {
            AdapterInfo.ActiveProtocolName = dpResp.Lines[0].Trim();
        }

        // Detect supported capabilities
        ElmCapabilities.SupportsCan = AdapterInfo.ActiveProtocol.IsCanProtocol();
        ElmCapabilities.Protocol = AdapterInfo.ActiveProtocol.ToAutomotiveProtocol();

        return true;
    }

    private static string ExtractElmVersion(string raw)
    {
        var match = Regex.Match(raw, @"(ELM327\s+v[0-9.]+|OBDLink\s+[A-Za-z0-9]+|STN[0-9]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "ELM327";
    }

    private static double? ParseVoltage(string raw)
    {
        var match = Regex.Match(raw, @"([0-9]+\.?[0-9]*)\s*V?", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            return Math.Round(v, 2);
        }
        return null;
    }

    public async Task DisconnectAsync()
    {
        lock (_lock)
        {
            _isConnected = false;
            ConnectionState = Elm327ConnectionState.Disconnected;
        }
        try
        {
            await _channel.CloseAsync();
        }
        catch
        {
            // Ignore close exceptions
        }
        NotifyStatusChanged();
    }

    private void SetError(string error)
    {
        lock (_lock)
        {
            _isConnected = false;
            ConnectionState = Elm327ConnectionState.Error;
            _lastError = error;
        }
        NotifyStatusChanged();
    }

    /// <summary>
    /// Sends an AT command or OBD/diagnostic request string to the ELM327 adapter and returns the parsed response.
    /// </summary>
    public async Task<Elm327Response> ExecuteElmCommandAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new Elm327Response { Success = false, ErrorMessage = "Command cannot be empty" };
        }

        TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(2);
        var resp = await SendCommandInternalAsync(command.Trim(), effectiveTimeout, cancellationToken);

        if (!resp.Success)
        {
            lock (_lock)
            {
                _lastError = resp.ErrorMessage;
            }
        }

        // Process any returned hex lines into canonical CanFrames
        foreach (var line in resp.Lines)
        {
            ProcessIncomingElmLine(line);
        }

        return resp;
    }

    private async Task<Elm327Response> SendCommandInternalAsync(string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = new Elm327Response();
        var startTime = DateTime.UtcNow;

        try
        {
            await _channel.SendLineAsync(command, cancellationToken);
            string raw = await _channel.ReadResponseAsync(timeout, cancellationToken);
            result.RawResponse = raw;
            result.Duration = DateTime.UtcNow - startTime;

            if (string.IsNullOrWhiteSpace(raw))
            {
                result.Success = false;
                result.Type = Elm327ResponseType.Timeout;
                result.ErrorMessage = "Timeout waiting for ELM327 response.";
                return result;
            }

            // Split into lines and strip prompt '>'
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => l.Replace(">", "").Trim())
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .ToList();

            result.Lines = lines;
            result.Type = ClassifyResponse(lines, raw);
            result.Success = (result.Type == Elm327ResponseType.Ok || result.Type == Elm327ResponseType.Data);

            if (!result.Success)
            {
                result.ErrorMessage = GetErrorMessageForType(result.Type, raw);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Type = Elm327ResponseType.Error;
            result.ErrorMessage = ex.Message;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    private static Elm327ResponseType ClassifyResponse(List<string> lines, string raw)
    {
        if (raw.Contains('?')) return Elm327ResponseType.UnknownCommand;
        if (raw.Contains("NO DATA", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.NoData;
        if (raw.Contains("CAN ERROR", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.CanError;
        if (raw.Contains("BUS BUSY", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.BusBusy;
        if (raw.Contains("BUFFER FULL", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.BufferFull;
        if (raw.Contains("DATA ERROR", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.DataError;
        if (raw.Contains("STOPPED", StringComparison.OrdinalIgnoreCase)) return Elm327ResponseType.Stopped;
        if (lines.Any(l => l.Equals("OK", StringComparison.OrdinalIgnoreCase))) return Elm327ResponseType.Ok;
        return Elm327ResponseType.Data;
    }

    private static string GetErrorMessageForType(Elm327ResponseType type, string raw) => type switch
    {
        Elm327ResponseType.UnknownCommand => "ELM327 returned '?' (unrecognized command or syntax error)",
        Elm327ResponseType.NoData => "No data response from vehicle bus (ECU did not respond)",
        Elm327ResponseType.CanError => "CAN bus transmission error reported by adapter",
        Elm327ResponseType.BusBusy => "Vehicle communication bus is busy",
        Elm327ResponseType.BufferFull => "ELM327 internal serial buffer overrun",
        Elm327ResponseType.DataError => "Data framing or checksum error on bus",
        Elm327ResponseType.Stopped => "Monitoring interrupted by operator or buffer limit",
        Elm327ResponseType.Timeout => "Response timeout",
        _ => raw.Trim()
    };

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isConnected) return ValueTask.FromResult(false);
            if (frame.IsCanFd || frame.Dlc > 8 || (frame.Data != null && frame.Data.Length > 8))
            {
                _lastError = "ELM327 only supports Classic CAN frames (DLC <= 8).";
                return ValueTask.FromResult(false);
            }

            _totalFramesTransmitted++;
            _windowFramesTransmitted++;
            _lastSeenUtc = DateTime.UtcNow;
        }

        // 1. Format outgoing frame in standard ELM327 AT format:
        // Set header: ATSH <CAN_ID_HEX>
        // Send payload: XX XX XX XX ...
        string headerCmd = frame.IsExtended
            ? $"ATSH {frame.Id:X8}"
            : $"ATSH {frame.Id:X3}";

        string payloadHex = frame.Data != null && frame.ActualLength > 0
            ? BitConverter.ToString(frame.Data, 0, frame.ActualLength).Replace("-", " ")
            : string.Empty;

        // 2. Transmit loopback notification so listeners observe outgoing TX frame
        var txFrame = frame;
        txFrame.Flags |= CanFrame.FlagTx;
        txFrame.SourceId = "ELM327-Tx";
        txFrame.Channel = 1;
        OnFrameReceived?.Invoke(txFrame);

        // 3. Send through channel
        _ = Task.Run(async () =>
        {
            try
            {
                await _channel.SendLineAsync(headerCmd, cancellationToken);
                await _channel.ReadResponseAsync(TimeSpan.FromMilliseconds(500), cancellationToken);

                await _channel.SendLineAsync(payloadHex, cancellationToken);
                string resp = await _channel.ReadResponseAsync(TimeSpan.FromSeconds(1), cancellationToken);

                if (!string.IsNullOrWhiteSpace(resp))
                {
                    var lines = resp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        ProcessIncomingElmLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
            }
        }, cancellationToken);

        // In simulated channel mode, ensure standard responses trigger
        if (_channel is SimulatedElmChannel)
        {
            SimulateLoopbackResponse(frame);
        }

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Parses an incoming ASCII line received from the ELM327 device into a canonical CanFrame.
    /// Supports 11-bit standard IDs, 29-bit extended IDs, and non-CAN diagnostic frames.
    /// </summary>
    public bool ProcessIncomingElmLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return false;

        string line = rawLine.Trim().Replace(">", "").Trim();
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Check for error lines
        if (line.Contains('?') ||
            line.Contains("NO DATA", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("CAN ERROR", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("BUS BUSY", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("BUFFER FULL", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("DATA ERROR", StringComparison.OrdinalIgnoreCase))
        {
            _lastError = line;
            return false;
        }

        // Skip AT command acknowledgments
        if (line.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("ELM327", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("SEARCHING", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("STOPPED", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("BUS INIT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Tokenize space-separated hex bytes
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            // Try compact hex string format (e.g. "7E8064100BE3FB811")
            if (line.Length >= 5 && Regex.IsMatch(line, "^[0-9A-Fa-f]+$"))
            {
                return ParseCompactHexLine(line);
            }
            return false;
        }

        // Check if this line is a raw legacy OBD diagnostic response without CAN headers
        // (e.g. in ISO 9141-2, KWP2000, J1850, or headers-off mode: "41 0C 1F 40", "41 00 ...")
        // Diagnostic response bytes have 2 hex digits and begin with 41-4F, 62, 7F, etc.
        bool isLegacyDiagnostic = (parts[0].Length == 2) &&
            (parts[0].StartsWith("4", StringComparison.OrdinalIgnoreCase) ||
             parts[0].StartsWith("6", StringComparison.OrdinalIgnoreCase) ||
             parts[0].StartsWith("7", StringComparison.OrdinalIgnoreCase)) &&
            parts.All(p => p.Length == 2);

        if (isLegacyDiagnostic)
        {
            return ParseLegacyDiagnosticLine(parts);
        }

        // First token is CAN ID
        if (!uint.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId))
        {
            return ParseLegacyDiagnosticLine(parts);
        }

        bool isExtended = (parts[0].Length > 3) || (canId > 0x7FF);
        var dataBytes = new List<byte>();

        for (int i = 1; i < parts.Length && dataBytes.Count < 8; i++)
        {
            if (byte.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                dataBytes.Add(b);
            }
        }

        if (dataBytes.Count == 0) return false;

        var frame = CanFrame.CreateClassic(
            id: canId,
            data: dataBytes.ToArray(),
            isExtended: isExtended,
            channel: 1,
            sourceId: "ELM327",
            direction: CanDirection.Rx
        );

        RecordReceivedFrame(frame);
        return true;
    }

    private bool ParseCompactHexLine(string line)
    {
        bool isExtended = line.Length > 19 || line.Length == 18;
        int idLen = isExtended ? 8 : 3;

        if (line.Length < idLen + 2) return false;

        string idHex = line.Substring(0, idLen);
        if (!uint.TryParse(idHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint canId))
        {
            return false;
        }

        string dataHex = line.Substring(idLen);
        var dataBytes = new List<byte>();
        for (int i = 0; i + 1 < dataHex.Length && dataBytes.Count < 8; i += 2)
        {
            if (byte.TryParse(dataHex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                dataBytes.Add(b);
            }
        }

        if (dataBytes.Count == 0) return false;

        var frame = CanFrame.CreateClassic(
            id: canId,
            data: dataBytes.ToArray(),
            isExtended: isExtended,
            channel: 1,
            sourceId: "ELM327",
            direction: CanDirection.Rx
        );

        RecordReceivedFrame(frame);
        return true;
    }

    private bool ParseLegacyDiagnosticLine(string[] tokens)
    {
        // When ELM327 operates in non-CAN mode (e.g. ISO 9141-2, KWP2000, or J1850)
        // without headers, responses look like: "41 00 BE 3F B8 11"
        var dataBytes = new List<byte>();
        foreach (var t in tokens)
        {
            if (byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                dataBytes.Add(b);
            }
        }

        if (dataBytes.Count == 0) return false;

        // Map into canonical diagnostic response frame (0x7E8) with ISO-TP SingleFrame length
        byte[] payload = new byte[Math.Min(8, dataBytes.Count + 1)];
        payload[0] = (byte)Math.Min(7, dataBytes.Count); // ISO-TP SF length
        for (int i = 0; i < dataBytes.Count && i + 1 < payload.Length; i++)
        {
            payload[i + 1] = dataBytes[i];
        }

        var frame = CanFrame.CreateClassic(
            id: 0x7E8,
            data: payload,
            isExtended: false,
            channel: 1,
            sourceId: $"ELM327-{AdapterInfo.ActiveProtocol}",
            direction: CanDirection.Rx
        );

        RecordReceivedFrame(frame);
        return true;
    }

    private void RecordReceivedFrame(CanFrame frame)
    {
        lock (_lock)
        {
            _totalFramesReceived++;
            _windowFramesReceived++;
            _lastSeenUtc = DateTime.UtcNow;
        }

        OnFrameReceived?.Invoke(frame);
    }

    private void SimulateLoopbackResponse(CanFrame request)
    {
        if (request.Id == 0x7DF || request.Id == 0x7E0)
        {
            uint respId = 0x7E8;
            byte[] respData = new byte[8];

            if (request.Data != null && request.Data.Length >= 3 && request.Data[1] == 0x01) // Mode 01
            {
                byte pid = request.Data[2];
                respData[0] = 0x04;
                respData[1] = 0x41;
                respData[2] = pid;

                if (pid == 0x0C) // RPM
                {
                    respData[0] = 0x04;
                    respData[3] = 0x1F;
                    respData[4] = 0x40;
                }
                else if (pid == 0x0D) // Speed
                {
                    respData[0] = 0x03;
                    respData[3] = 0x41;
                }
                else if (pid == 0x05) // Coolant Temp
                {
                    respData[0] = 0x03;
                    respData[3] = 0x82;
                }
                else
                {
                    respData[0] = 0x03;
                    respData[3] = 0x00;
                }
            }
            else
            {
                respData[0] = 0x02;
                respData[1] = 0x50;
                respData[2] = 0x01;
            }

            var respFrame = CanFrame.CreateClassic(
                id: respId,
                data: respData,
                isExtended: request.IsExtended,
                channel: 1,
                sourceId: "ELM327",
                direction: CanDirection.Rx
            );

            RecordReceivedFrame(respFrame);
        }
    }

    /// <summary>
    /// Scans the operating system for available serial ports and Bluetooth virtual COM ports.
    /// </summary>
    public static IReadOnlyList<string> GetAvailablePorts()
    {
        var list = new List<string>();
        try
        {
            list.AddRange(SerialPort.GetPortNames().Distinct().OrderBy(p => p));
        }
        catch
        {
            // Port discovery not available
        }

        if (!list.Contains("Loopback", StringComparer.OrdinalIgnoreCase))
        {
            list.Add("Loopback (Simulated)");
        }
        return list;
    }

    public HardwareStatus GetStatus()
    {
        lock (_lock)
        {
            double seconds = (DateTime.UtcNow - _lastFpsCalc).TotalSeconds;
            if (seconds >= 1.0)
            {
                _rxFrameRate = Math.Round(_windowFramesReceived / seconds, 1);
                _txFrameRate = Math.Round(_windowFramesTransmitted / seconds, 1);
                _windowFramesReceived = 0;
                _windowFramesTransmitted = 0;
                _lastFpsCalc = DateTime.UtcNow;
            }

            return new HardwareStatus
            {
                TransportId = TransportId,
                Name = Name,
                Type = Type,
                IsConnected = _isConnected,
                ConnectionDetails = $"{PortName} @ {SerialBaudRate} baud | {AdapterInfo.ActiveProtocolName} | Voltage: {(AdapterInfo.BatteryVoltage.HasValue ? AdapterInfo.BatteryVoltage.Value + "V" : "N/A")}",
                TotalFramesReceived = _totalFramesReceived,
                TotalFramesTransmitted = _totalFramesTransmitted,
                RxFrameRate = _rxFrameRate,
                TxFrameRate = _txFrameRate,
                LastSeenUtc = _lastSeenUtc,
                LastError = _lastError,
                Capabilities = Capabilities,
                ExtendedTelemetry = new
                {
                    PortName,
                    SerialBaudRate,
                    NominalBaudRate,
                    ElmVersion = AdapterInfo.FirmwareVersion,
                    DeviceName = AdapterInfo.DeviceName,
                    Description = AdapterInfo.Description,
                    BatteryVoltage = AdapterInfo.BatteryVoltage,
                    ActiveProtocol = AdapterInfo.ActiveProtocolName,
                    ActiveProtocolCode,
                    ConnectionState = ConnectionState.ToString(),
                    ConnectionType = AdapterInfo.ConnectionType,
                    AutoReconnect,
                    ReconnectAttempts = AdapterInfo.ReconnectAttempts,
                    SupportedProtocols = ElmCapabilities.SupportedProtocols.Select(p => p.GetDescription()).ToList()
                }
            };
        }
    }

    public AutomotiveTransportStatus GetAutomotiveStatus()
    {
        var hwStatus = GetStatus();
        return new AutomotiveTransportStatus
        {
            TransportId = hwStatus.TransportId,
            Name = hwStatus.Name,
            Protocol = Protocol,
            State = State,
            ConnectionDetails = hwStatus.ConnectionDetails,
            TotalPacketsReceived = hwStatus.TotalFramesReceived,
            TotalPacketsTransmitted = hwStatus.TotalFramesTransmitted,
            RxPacketRate = hwStatus.RxFrameRate,
            TxPacketRate = hwStatus.TxFrameRate,
            LastSeenUtc = hwStatus.LastSeenUtc,
            LastError = hwStatus.LastError,
            Capabilities = ElmCapabilities,
            ExtendedTelemetry = hwStatus.ExtendedTelemetry
        };
    }

    private void NotifyStatusChanged()
    {
        var hwStatus = GetStatus();
        OnStatusChanged?.Invoke(hwStatus);
        OnAutomotiveStatusChanged?.Invoke(GetAutomotiveStatus());
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        if (_ownsChannel)
        {
            _channel.Dispose();
        }
    }
}
