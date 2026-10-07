using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Hardware;

public class HardwareManager : IAutomotiveHardwareManager
{
    private readonly ICanPipeline? _pipeline;
    private readonly ConcurrentDictionary<string, ICanTransport> _canTransports = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IAutomotiveTransport> _automotiveTransports = new(StringComparer.OrdinalIgnoreCase);
    private ICanTransport? _activeTransport;
    private readonly object _lock = new();

    public event Action<ICanTransport?>? OnActiveTransportChanged;
    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnHardwareStatusChanged;
    public event Action<IAutomotiveTransport>? OnAutomotiveTransportRegistered;

    public string? ActiveTransportId
    {
        get
        {
            lock (_lock)
            {
                return _activeTransport?.TransportId;
            }
        }
    }

    public HardwareManager(ICanPipeline? pipeline = null, IEnumerable<ICanTransport>? initialTransports = null)
    {
        _pipeline = pipeline;

        if (initialTransports != null)
        {
            foreach (var transport in initialTransports)
            {
                RegisterTransport(transport);
            }
        }
    }

    public void RegisterTransport(ICanTransport transport)
    {
        if (transport == null) return;

        if (_canTransports.TryAdd(transport.TransportId, transport))
        {
            _automotiveTransports.TryAdd(transport.TransportId, transport);

            // Register compatibility aliases
            if (transport.TransportId.Equals("waveshare-usb-can-fd", StringComparison.OrdinalIgnoreCase))
            {
                _canTransports.TryAdd("waveshare-canfd-b", transport);
                _automotiveTransports.TryAdd("waveshare-canfd-b", transport);
            }
            else if (transport.TransportId.Equals("waveshare-canfd-b", StringComparison.OrdinalIgnoreCase))
            {
                _canTransports.TryAdd("waveshare-usb-can-fd", transport);
                _automotiveTransports.TryAdd("waveshare-usb-can-fd", transport);
            }

            transport.OnFrameReceived += HandleTransportFrame;
            transport.OnStatusChanged += HandleTransportStatus;

            lock (_lock)
            {
                // Set first registered transport as active by default if none active
                if (_activeTransport == null)
                {
                    _activeTransport = transport;
                }
            }

            OnAutomotiveTransportRegistered?.Invoke(transport);
        }
    }

    public void RegisterAutomotiveTransport(IAutomotiveTransport transport)
    {
        if (transport == null) return;

        if (transport is ICanTransport canTransport)
        {
            RegisterTransport(canTransport);
            return;
        }

        if (_automotiveTransports.TryAdd(transport.TransportId, transport))
        {
            OnAutomotiveTransportRegistered?.Invoke(transport);
        }
    }

    public IReadOnlyList<ICanTransport> GetAllTransports()
    {
        return _canTransports.Values.Distinct().ToList();
    }

    public IReadOnlyList<IAutomotiveTransport> GetAllAutomotiveTransports()
    {
        return _automotiveTransports.Values.Distinct().ToList();
    }

    public IReadOnlyList<TTransport> GetTransportsByProtocol<TTransport>(AutomotiveProtocol protocol) where TTransport : class, IAutomotiveTransport
    {
        return _automotiveTransports.Values
            .Where(t => t.Protocol == protocol)
            .OfType<TTransport>()
            .ToList();
    }

    public IAutomotiveTransport? GetAutomotiveTransport(string transportId)
    {
        if (string.IsNullOrWhiteSpace(transportId)) return null;
        _automotiveTransports.TryGetValue(transportId, out var transport);
        return transport;
    }

    public ICanTransport? GetActiveTransport()
    {
        lock (_lock)
        {
            return _activeTransport;
        }
    }

    public ICanTransport? GetTransport(string transportId)
    {
        if (string.IsNullOrWhiteSpace(transportId)) return null;
        _canTransports.TryGetValue(transportId, out var transport);
        return transport;
    }

    public async Task<bool> SetActiveTransportAsync(string transportId, CancellationToken cancellationToken = default)
    {
        if (!_canTransports.TryGetValue(transportId, out var newTransport))
        {
            return false;
        }

        ICanTransport? previousTransport = null;
        bool alreadyActive = false;

        lock (_lock)
        {
            if (ReferenceEquals(_activeTransport, newTransport))
            {
                alreadyActive = true;
            }
            else
            {
                previousTransport = _activeTransport;
                _activeTransport = newTransport;
            }
        }

        if (alreadyActive)
        {
            if (!newTransport.GetStatus().IsConnected)
            {
                await newTransport.ConnectAsync(cancellationToken);
                OnHardwareStatusChanged?.Invoke(newTransport.GetStatus());
            }
            return true;
        }

        // If both are physical listening hardware (e.g. GVRET and ESP8266 USB), preserve concurrent reception
        bool isDualHardwareSwitch = (previousTransport is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport && newTransport is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport)
                                 || (previousTransport is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport && newTransport is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport);

        if (previousTransport != null && !isDualHardwareSwitch)
        {
            await previousTransport.DisconnectAsync();
        }

        if (!newTransport.GetStatus().IsConnected)
        {
            await newTransport.ConnectAsync(cancellationToken);
        }

        OnActiveTransportChanged?.Invoke(newTransport);
        OnHardwareStatusChanged?.Invoke(newTransport.GetStatus());
        return true;
    }

    public async Task<bool> ConnectTransportAsync(string transportId, CancellationToken cancellationToken = default)
    {
        if (!_canTransports.TryGetValue(transportId, out var transport))
        {
            return false;
        }

        if (!transport.GetStatus().IsConnected)
        {
            await transport.ConnectAsync(cancellationToken);
            OnHardwareStatusChanged?.Invoke(transport.GetStatus());
        }
        return true;
    }

    public async Task<bool> DisconnectTransportAsync(string transportId, CancellationToken cancellationToken = default)
    {
        if (!_canTransports.TryGetValue(transportId, out var transport))
        {
            return false;
        }

        if (transport.GetStatus().IsConnected)
        {
            await transport.DisconnectAsync();
            OnHardwareStatusChanged?.Invoke(transport.GetStatus());
        }
        return true;
    }

    public async Task<bool> EnableDualHardwareStreamingAsync(string gvretPort = "COM8", string espPort = "COM14", CancellationToken cancellationToken = default)
    {
        bool gvretOk = false;
        bool espOk = false;

        // 1. Configure and connect Arduino Nano GVRET (Channel 0)
        var gvretTransport = GetTransport(CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport.TransportIdentifier)
                          ?? GetAllTransports().FirstOrDefault(t => t is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport);
        if (gvretTransport is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport gvret)
        {
            gvret.Configure(gvretPort, 500000u, true, 1000000);
            if (!gvret.GetStatus().IsConnected)
            {
                gvretOk = await gvret.ConnectAsync(cancellationToken);
            }
            else
            {
                gvretOk = true;
            }
            OnHardwareStatusChanged?.Invoke(gvret.GetStatus());
        }

        // 2. Configure and connect ESP8266 USB Serial (Channel 1)
        var espTransport = GetTransport("esp8266-usb-serial")
                        ?? GetAllTransports().FirstOrDefault(t => t is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport);
        if (espTransport is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport espUsb)
        {
            espUsb.Configure(espPort, 1);
            if (!espUsb.GetStatus().IsConnected)
            {
                espOk = await espUsb.ConnectAsync(cancellationToken);
            }
            else
            {
                espOk = true;
            }
            OnHardwareStatusChanged?.Invoke(espUsb.GetStatus());
        }

        return gvretOk || espOk;
    }

    public IReadOnlyList<ICanTransport> GetConnectedTransports()
    {
        return _canTransports.Values.Distinct().Where(t => t.GetStatus().IsConnected).ToList();
    }

    public HardwareStatus? GetStatus()
    {
        lock (_lock)
        {
            return _activeTransport?.GetStatus();
        }
    }

    public async Task<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        ICanTransport? transport;
        lock (_lock)
        {
            transport = _activeTransport;
        }

        if (transport == null || !transport.Capabilities.SupportsTransmit)
        {
            return false;
        }

        return await transport.SendFrameAsync(frame, cancellationToken);
    }

    private void HandleTransportFrame(CanFrame frame)
    {
        OnFrameReceived?.Invoke(frame);
        _pipeline?.PublishFrameAsync(frame);
    }

    private void HandleTransportStatus(HardwareStatus status)
    {
        OnHardwareStatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        // Use Distinct() — alias keys (e.g. waveshare-canfd-b) point to the same
        // transport instance as their canonical key, so Values may contain duplicates.
        foreach (var transport in _canTransports.Values.Distinct())
        {
            transport.OnFrameReceived -= HandleTransportFrame;
            transport.OnStatusChanged -= HandleTransportStatus;
            transport.Dispose();
        }
        _canTransports.Clear();

        foreach (var transport in _automotiveTransports.Values.Distinct())
        {
            if (!_canTransports.ContainsKey(transport.TransportId))
            {
                transport.Dispose();
            }
        }
        _automotiveTransports.Clear();
    }
}
