using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Decoders;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware.Transports;

public class Esp8266Transport : ICanTransport
{
    public string TransportId => "esp8266-gateway";
    public string Name => "ESP8266 + MCP2515 CAN Gateway";
    public TransportType Type => TransportType.Esp8266;

    public HardwareCapabilities Capabilities { get; } = new()
    {
        SupportsCanFd = false,
        SupportsBitRateSwitch = false,
        SupportsClassicCan = true,
        ChannelCount = 1,
        MaxPayloadBytes = 8,
        SupportsTransmit = false, // Listen-only mode as required by Phase 1 safety
        SupportsHardwareTimestamps = true,
        SupportedNominalBaudRates = new uint[] { 500000 },
        SupportedDataBaudRates = Array.Empty<uint>()
    };

    private string _gatewayAddress = "192.168.4.1:8088";
    private readonly object _lock = new();
    private ClientWebSocket? _client;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoopTask;

    private bool _isConnected;
    private long _totalFramesReceived;
    private int _windowFramesReceived;
    private double _rxFrameRate;
    private DateTime? _lastSeenUtc;
    private string? _lastError;
    private JsonElement? _espTelemetry;
    private DateTime _lastFpsCalc = DateTime.UtcNow;

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;

    public Esp8266Transport(string? initialAddress = null)
    {
        if (!string.IsNullOrWhiteSpace(initialAddress))
        {
            _gatewayAddress = initialAddress.Trim();
        }
    }

    public void ConfigureAddress(string address)
    {
        lock (_lock)
        {
            _gatewayAddress = address.Trim();
            _client?.Abort();
        }
        NotifyStatusChanged();
    }

    public string GatewayAddress
    {
        get
        {
            lock (_lock) return _gatewayAddress;
        }
    }

    public JsonElement? EspTelemetry
    {
        get
        {
            lock (_lock) return _espTelemetry;
        }
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
                IsConnected = _isConnected,
                ConnectionDetails = $"ws://{_gatewayAddress}",
                TotalFramesReceived = _totalFramesReceived,
                TotalFramesTransmitted = 0,
                RxFrameRate = _rxFrameRate,
                TxFrameRate = 0,
                LastSeenUtc = _lastSeenUtc,
                LastError = _lastError,
                Capabilities = Capabilities,
                ExtendedTelemetry = _espTelemetry
            };
        }
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isConnected) return Task.FromResult(true);

            _connectionCts?.Cancel();
            _connectionCts = new CancellationTokenSource();
            _receiveLoopTask = Task.Run(() => ConnectionLoopAsync(_connectionCts.Token));
        }

        return Task.FromResult(true);
    }

    public async Task DisconnectAsync()
    {
        CancellationTokenSource? cts;
        ClientWebSocket? ws;

        lock (_lock)
        {
            cts = _connectionCts;
            ws = _client;
            _isConnected = false;
        }

        cts?.Cancel();
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }

        NotifyStatusChanged();
    }

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        // Phase 1 Hardware Gateway is strictly listen-only
        throw new NotSupportedException("ESP8266 + MCP2515 hardware gateway is configured in listen-only mode and does not support frame transmission.");
    }

    public async Task RequestEspStatusAsync()
    {
        ClientWebSocket? ws;
        lock (_lock)
        {
            ws = _client;
        }

        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                byte[] command = Encoding.UTF8.GetBytes("status");
                await ws.SendAsync(new ArraySegment<byte>(command), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _lastError = ex.Message;
                }
            }
        }
    }

    private async Task ConnectionLoopAsync(CancellationToken stoppingToken)
    {
        var receiveBuffer = new byte[4096];
        var textBuffer = new MemoryStream();

        while (!stoppingToken.IsCancellationRequested)
        {
            string currentAddr;
            lock (_lock)
            {
                currentAddr = _gatewayAddress;
            }

            using var ws = new ClientWebSocket();
            lock (_lock)
            {
                _client = ws;
            }

            try
            {
                var uri = new Uri(currentAddr.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                                  currentAddr.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)
                    ? currentAddr
                    : $"ws://{currentAddr}");

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                connectCts.CancelAfter(4000);
                await ws.ConnectAsync(uri, connectCts.Token);

                lock (_lock)
                {
                    _isConnected = true;
                    _lastError = null;
                }
                NotifyStatusChanged();

                _ = RequestEspStatusAsync();
                DateTime lastStatusPoll = DateTime.UtcNow;

                while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), stoppingToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        ProcessBinaryPacket(receiveBuffer.AsSpan(0, result.Count));
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        textBuffer.Write(receiveBuffer, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            string text = Encoding.UTF8.GetString(textBuffer.ToArray());
                            textBuffer.SetLength(0);
                            ProcessTextResponse(text);
                        }
                    }

                    if ((DateTime.UtcNow - lastStatusPoll).TotalSeconds >= 2.0)
                    {
                        _ = RequestEspStatusAsync();
                        lastStatusPoll = DateTime.UtcNow;
                    }

                    var now = DateTime.UtcNow;
                    var fpsElapsed = (now - _lastFpsCalc).TotalSeconds;
                    if (fpsElapsed >= 1.0)
                    {
                        lock (_lock)
                        {
                            _rxFrameRate = Math.Round(_windowFramesReceived / fpsElapsed, 1);
                            _windowFramesReceived = 0;
                            _lastFpsCalc = now;
                        }
                        NotifyStatusChanged();
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _isConnected = false;
                    _lastError = ex.Message;
                }
                NotifyStatusChanged();
            }
            finally
            {
                lock (_lock)
                {
                    _isConnected = false;
                }
                NotifyStatusChanged();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void ProcessBinaryPacket(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;
        while (offset + BinaryPacketDecoder.PacketSize <= buffer.Length)
        {
            var chunk = buffer.Slice(offset, BinaryPacketDecoder.PacketSize);
            if (BinaryPacketDecoder.TryDecode(chunk, out var frame))
            {
                // Decorate with transport source and channel
                frame.SourceId = "ESP8266";
                frame.Channel = 0;

                lock (_lock)
                {
                    _totalFramesReceived++;
                    _windowFramesReceived++;
                    _lastSeenUtc = DateTime.UtcNow;
                }

                OnFrameReceived?.Invoke(frame);
            }
            offset += BinaryPacketDecoder.PacketSize;
        }
    }

    private void ProcessTextResponse(string jsonText)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            lock (_lock)
            {
                _espTelemetry = doc.RootElement.Clone();
            }
            NotifyStatusChanged();
        }
        catch
        {
            // Ignore non-JSON debug strings
        }
    }

    private void NotifyStatusChanged()
    {
        OnStatusChanged?.Invoke(GetStatus());
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _connectionCts?.Cancel();
        }
        catch
        {
        }
        _client?.Abort();
        _client?.Dispose();
        _connectionCts?.Dispose();
    }
}
