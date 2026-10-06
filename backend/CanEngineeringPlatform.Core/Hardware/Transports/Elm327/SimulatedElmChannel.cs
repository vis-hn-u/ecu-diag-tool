using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CanEngineeringPlatform.Core.Hardware.Transports.Elm327;

/// <summary>
/// High-fidelity in-memory simulated ELM327 device channel for unit testing, integration tests,
/// and demonstration when physical Bluetooth hardware is not connected.
/// </summary>
public class SimulatedElmChannel : IElm327CommunicationChannel
{
    public string ChannelName => "SimulatedElm327";
    public bool IsOpen { get; private set; }

    public string DeviceVersion { get; set; } = "ELM327 v1.5";
    public string DeviceDescription { get; set; } = "OBDLink LX Bluetooth";
    public double BatteryVoltage { get; set; } = 12.6;
    public Elm327Protocol CurrentProtocol { get; set; } = Elm327Protocol.Iso15765_4_Can_11Bit_500k;
    public bool HeadersOn { get; set; } = true;
    public bool EchoOn { get; set; } = false;

    // Fault injection for testing
    public bool InjectTimeout { get; set; }
    public string? InjectedErrorResponse { get; set; }
    public readonly ConcurrentQueue<string> SentCommands = new();
    private readonly ConcurrentDictionary<string, string> _customHandlers = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> OpenAsync(CancellationToken cancellationToken = default)
    {
        IsOpen = true;
        return Task.FromResult(true);
    }

    public Task CloseAsync()
    {
        IsOpen = false;
        return Task.CompletedTask;
    }

    public Task SendLineAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsOpen) throw new InvalidOperationException("Simulated ELM channel is closed.");
        string clean = command.Trim().ToUpperInvariant();
        SentCommands.Enqueue(clean);
        return Task.CompletedTask;
    }

    public void RegisterCustomResponse(string commandPrefix, string response)
    {
        _customHandlers[commandPrefix.Trim().ToUpperInvariant()] = response;
    }

    public async Task<string> ReadResponseAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsOpen) throw new InvalidOperationException("Simulated ELM channel is closed.");

        if (InjectTimeout)
        {
            await Task.Delay(timeout, cancellationToken);
            return string.Empty;
        }

        if (InjectedErrorResponse != null)
        {
            string err = InjectedErrorResponse;
            InjectedErrorResponse = null; // one-shot
            return $"\r{err}\r\r>";
        }

        if (!SentCommands.TryDequeue(out string? cmd) || string.IsNullOrWhiteSpace(cmd))
        {
            return "\r?\r\r>";
        }

        // Custom injected handler check
        foreach (var kvp in _customHandlers)
        {
            if (cmd.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return $"\r{kvp.Value}\r\r>";
            }
        }

        // Standard ELM327 command emulation
        if (cmd == "ATZ")
        {
            return $"\r\r{DeviceVersion}\r\r>";
        }
        if (cmd == "ATI")
        {
            return $"\r{DeviceVersion}\r\r>";
        }
        if (cmd == "AT@1")
        {
            return $"\r{DeviceDescription}\r\r>";
        }
        if (cmd == "AT@2")
        {
            return "\rSIMULATED-DEVICE-ID-99\r\r>";
        }
        if (cmd == "ATRV")
        {
            return $"\r{BatteryVoltage:F1}V\r\r>";
        }
        if (cmd == "ATE0")
        {
            EchoOn = false;
            return "\rOK\r\r>";
        }
        if (cmd == "ATE1")
        {
            EchoOn = true;
            return "\rOK\r\r>";
        }
        if (cmd == "ATL0" || cmd == "ATL1" || cmd == "ATS0" || cmd == "ATS1" || cmd == "ATAT1" || cmd == "ATAT2")
        {
            return "\rOK\r\r>";
        }
        if (cmd == "ATH1")
        {
            HeadersOn = true;
            return "\rOK\r\r>";
        }
        if (cmd == "ATH0")
        {
            HeadersOn = false;
            return "\rOK\r\r>";
        }
        if (cmd.StartsWith("ATSP"))
        {
            string protoCode = cmd.Substring(4).Trim();
            CurrentProtocol = Elm327ProtocolExtensions.FromProtocolCode(protoCode);
            return "\rOK\r\r>";
        }
        if (cmd == "ATDP")
        {
            return $"\r{CurrentProtocol.GetDescription()}\r\r>";
        }
        if (cmd == "ATDPN")
        {
            return $"\rA{CurrentProtocol.ToProtocolCode()}\r\r>";
        }
        if (cmd.StartsWith("ATSH"))
        {
            return "\rOK\r\r>";
        }

        // Diagnostic PID requests (OBD-II Mode 01)
        if (cmd == "0100")
        {
            // Supported PIDs 01-20
            return HeadersOn
                ? "\r7E8 06 41 00 BE 3F B8 11\r\r>"
                : "\r41 00 BE 3F B8 11\r\r>";
        }
        if (cmd == "010C")
        {
            // Engine RPM: ~2000 RPM ((0x1F40)/4 = 2000)
            return HeadersOn
                ? "\r7E8 04 41 0C 1F 40\r\r>"
                : "\r41 0C 1F 40\r\r>";
        }
        if (cmd == "010D")
        {
            // Vehicle Speed: 65 km/h (0x41)
            return HeadersOn
                ? "\r7E8 03 41 0D 41\r\r>"
                : "\r41 0D 41\r\r>";
        }
        if (cmd == "0105")
        {
            // Engine Coolant: 90°C (0x82 = 130 - 40 = 90)
            return HeadersOn
                ? "\r7E8 03 41 05 82\r\r>"
                : "\r41 05 82\r\r>";
        }
        if (cmd == "0101")
        {
            // Monitor status since DTCs cleared: MIL off, 0 DTCs
            return HeadersOn
                ? "\r7E8 06 41 01 00 07 E5 00\r\r>"
                : "\r41 01 00 07 E5 00\r\r>";
        }
        if (cmd == "03")
        {
            // Mode 03: Request Stored Trouble Codes (No DTCs reported)
            return HeadersOn
                ? "\r7E8 02 43 00\r\r>"
                : "\r43 00\r\r>";
        }
        if (cmd == "22F190")
        {
            // UDS ReadDataByIdentifier VIN
            return HeadersOn
                ? "\r7E8 10 14 62 F1 90 31 46 41\r7E8 21 36 50 38 43 46 35 48\r7E8 22 35 31 32 33 34 35 36\r\r>"
                : "\r62 F1 90 31 46 41 36 50 38 43 46 35 48 35 31 32 33 34 35 36\r\r>";
        }

        // Default positive acknowledgment
        return "\rOK\r\r>";
    }

    public void Drain()
    {
        while (SentCommands.TryDequeue(out _)) { }
    }

    public void Dispose()
    {
        IsOpen = false;
        Drain();
    }
}
