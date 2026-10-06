using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;

public interface ILiveDataService : IDisposable
{
    IReadOnlyList<LiveDataParameterDefinition> GetSupportedDefinitions();
    IReadOnlyList<LiveDataSample> GetLatestSamples();
    LiveDataSample? GetSample(string parameterIdOrName);
    void IngestSample(LiveDataSample sample);
    Task<LiveDataSample?> PollParameterAsync(uint requestCanId, string parameterId, string ecuName = "ECM", CancellationToken cancellationToken = default);
    void StartPolling(uint requestCanId, IEnumerable<string> parameterIds, int intervalMs = 250, string ecuName = "ECM");
    void StopPolling();
    event Action<IReadOnlyList<LiveDataSample>>? OnSamplesUpdated;
}

public class LiveDataService : ILiveDataService
{
    private readonly IDiagnosticSessionService _sessionService;
    private readonly IDiagnosticSessionManager _sessionManager;
    private readonly IDiagnosticTraceService _traceService;

    private readonly ConcurrentDictionary<string, LiveDataParameterDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LiveDataSample> _latestSamples = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LiveDataSample> _previousSamples = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private bool _disposed;

    public event Action<IReadOnlyList<LiveDataSample>>? OnSamplesUpdated;

    public LiveDataService(
        IDiagnosticSessionService sessionService,
        IDiagnosticSessionManager sessionManager,
        IDiagnosticTraceService traceService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));

        RegisterDefaultDefinitions();
    }

    private void RegisterDefaultDefinitions()
    {
        // Engine RPM (DID 0x0100 / OBD PID 0x0C)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "ENGINE_RPM",
            Name = "Engine Speed",
            Category = "Engine",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0100,
            ObdPid = 0x0C,
            Unit = "RPM",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 0.0,
            MaxExpectedValue = 8500.0,
            MaxAllowableJumpPerSecond = 5000.0,
            Description = "Crankshaft rotational velocity"
        });

        // Engine Coolant Temperature (DID 0x0101 / OBD PID 0x05)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "COOLANT_TEMP",
            Name = "Engine Coolant Temperature",
            Category = "Thermal",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0101,
            ObdPid = 0x05,
            Unit = "°C",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = -40.0,
            MaxExpectedValue = 135.0,
            MaxAllowableJumpPerSecond = 20.0,
            Description = "Primary engine block coolant temperature"
        });

        // Battery / Module Voltage (DID 0x0102 / OBD PID 0x42)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "MODULE_VOLTAGE",
            Name = "Battery / System Voltage",
            Category = "Electrical",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0102,
            ObdPid = 0x42,
            Unit = "V",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 9.0,
            MaxExpectedValue = 16.5,
            MaxAllowableJumpPerSecond = 6.0,
            Description = "ECU supply voltage measured at terminal 30/15"
        });

        // Throttle Position (DID 0x0103 / OBD PID 0x11)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "THROTTLE_POS",
            Name = "Throttle Position",
            Category = "Air / Induction",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0103,
            ObdPid = 0x11,
            Unit = "%",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 0.0,
            MaxExpectedValue = 100.0,
            MaxAllowableJumpPerSecond = 300.0,
            Description = "Absolute throttle blade angle"
        });

        // Mass Air Flow Rate (DID 0x0104 / OBD PID 0x10)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "MASS_AIR_FLOW",
            Name = "Mass Air Flow",
            Category = "Air / Induction",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0104,
            ObdPid = 0x10,
            Unit = "g/s",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 0.0,
            MaxExpectedValue = 450.0,
            MaxAllowableJumpPerSecond = 200.0,
            Description = "Intake air mass ingested per second"
        });

        // Manifold Absolute Pressure (DID 0x0105 / OBD PID 0x0B)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "MANIFOLD_PRESSURE",
            Name = "Manifold Absolute Pressure",
            Category = "Air / Induction",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0105,
            ObdPid = 0x0B,
            Unit = "kPa",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 10.0,
            MaxExpectedValue = 350.0,
            MaxAllowableJumpPerSecond = 250.0,
            Description = "Absolute pressure in intake plenum"
        });

        // O2 Lambda Sensor (DID 0x0106 / OBD PID 0x24)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "LAMBDA",
            Name = "Air-Fuel Equivalence Ratio (Lambda)",
            Category = "Combustion",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0106,
            ObdPid = 0x24,
            Unit = "λ",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 0.65,
            MaxExpectedValue = 1.50,
            MaxAllowableJumpPerSecond = 1.0,
            Description = "Wideband lambda equivalence ratio"
        });

        // Short Term Fuel Trim (DID 0x0107 / OBD PID 0x06)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "STFT",
            Name = "Short Term Fuel Trim Bank 1",
            Category = "Combustion",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0107,
            ObdPid = 0x06,
            Unit = "%",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = -35.0,
            MaxExpectedValue = 35.0,
            MaxAllowableJumpPerSecond = 40.0,
            Description = "Immediate closed-loop fueling trim adjustment"
        });

        // Long Term Fuel Trim (DID 0x0108 / OBD PID 0x07)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "LTFT",
            Name = "Long Term Fuel Trim Bank 1",
            Category = "Combustion",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0108,
            ObdPid = 0x07,
            Unit = "%",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = -30.0,
            MaxExpectedValue = 30.0,
            MaxAllowableJumpPerSecond = 10.0,
            Description = "Learned steady-state fueling trim offset"
        });

        // Vehicle Speed (DID 0x0109 / OBD PID 0x0D)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "VEHICLE_SPEED",
            Name = "Vehicle Speed",
            Category = "Driveline",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x0109,
            ObdPid = 0x0D,
            Unit = "km/h",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = 0.0,
            MaxExpectedValue = 320.0,
            MaxAllowableJumpPerSecond = 50.0,
            Description = "Road wheel translational velocity"
        });

        // Intake Air Temperature (DID 0x010A / OBD PID 0x0F)
        RegisterDefinition(new LiveDataParameterDefinition
        {
            Id = "INTAKE_AIR_TEMP",
            Name = "Intake Air Temperature",
            Category = "Thermal",
            Source = LiveDataProtocolSource.UdsDid,
            DataIdentifier = 0x010A,
            ObdPid = 0x0F,
            Unit = "°C",
            Scaling = 1.0,
            Offset = 0.0,
            MinExpectedValue = -40.0,
            MaxExpectedValue = 90.0,
            MaxAllowableJumpPerSecond = 15.0,
            Description = "Intake manifold ambient charge air temperature"
        });
    }

    public void RegisterDefinition(LiveDataParameterDefinition def)
    {
        _definitions[def.Id] = def;
    }

    public IReadOnlyList<LiveDataParameterDefinition> GetSupportedDefinitions()
    {
        return _definitions.Values.OrderBy(d => d.Category).ThenBy(d => d.Name).ToList();
    }

    public IReadOnlyList<LiveDataSample> GetLatestSamples()
    {
        var now = DateTime.UtcNow;
        var list = new List<LiveDataSample>();

        foreach (var sample in _latestSamples.Values)
        {
            // Evaluate staleness dynamically (e.g. older than 3.0s)
            var age = (now - sample.TimestampUtc).TotalSeconds;
            if (age > 3.0 && sample.Quality == DataQuality.Valid)
            {
                sample.Quality = DataQuality.Stale;
                sample.QualityReason = $"Data age ({age:F1}s) exceeds staleness window.";
            }
            list.Add(sample);
        }

        return list.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
    }

    public LiveDataSample? GetSample(string parameterIdOrName)
    {
        if (_latestSamples.TryGetValue(parameterIdOrName, out var sample))
        {
            return sample;
        }

        var match = _latestSamples.Values.FirstOrDefault(s =>
            s.Name.Equals(parameterIdOrName, StringComparison.OrdinalIgnoreCase) ||
            s.ParameterId.Equals(parameterIdOrName, StringComparison.OrdinalIgnoreCase));

        return match;
    }

    public void IngestSample(LiveDataSample sample)
    {
        if (sample == null) return;

        // Perform quality validations
        if (_definitions.TryGetValue(sample.ParameterId, out var def))
        {
            if (sample.ScaledValue < def.MinExpectedValue || sample.ScaledValue > def.MaxExpectedValue)
            {
                sample.Quality = DataQuality.OutOfRange;
                sample.QualityReason = $"Value {sample.ScaledValue:F2} {def.Unit} is outside physical bounds [{def.MinExpectedValue}..{def.MaxExpectedValue}].";
            }
            else if (_previousSamples.TryGetValue(sample.ParameterId, out var prev))
            {
                var dt = (sample.TimestampUtc - prev.TimestampUtc).TotalSeconds;
                if (dt > 0.001 && def.MaxAllowableJumpPerSecond.HasValue)
                {
                    var rate = Math.Abs(sample.ScaledValue - prev.ScaledValue) / dt;
                    if (rate > def.MaxAllowableJumpPerSecond.Value)
                    {
                        sample.Quality = DataQuality.Invalid;
                        sample.QualityReason = $"Rate of change ({rate:F1} {def.Unit}/s) exceeds physical sensor limit ({def.MaxAllowableJumpPerSecond.Value} {def.Unit}/s).";
                    }
                }
            }
        }

        _previousSamples[sample.ParameterId] = sample;
        _latestSamples[sample.ParameterId] = sample;

        OnSamplesUpdated?.Invoke(GetLatestSamples());
    }

    public async Task<LiveDataSample?> PollParameterAsync(
        uint requestCanId,
        string parameterId,
        string ecuName = "ECM",
        CancellationToken cancellationToken = default)
    {
        if (!_definitions.TryGetValue(parameterId, out var def))
        {
            return null;
        }

        byte[] requestPayload;
        if (def.Source == LiveDataProtocolSource.UdsDid && def.DataIdentifier.HasValue)
        {
            ushort did = def.DataIdentifier.Value;
            requestPayload = new byte[] { (byte)UdsServiceId.ReadDataByIdentifier, (byte)(did >> 8), (byte)(did & 0xFF) };
        }
        else if (def.ObdPid.HasValue)
        {
            requestPayload = new byte[] { 0x01, def.ObdPid.Value };
        }
        else
        {
            return null;
        }

        var sw = Stopwatch.StartNew();
        var exchange = await _sessionService.SendRequestAsync(requestCanId, requestPayload, ecuName, isSimulation: true);

        int waited = 0;
        while (!exchange.IsComplete && waited < 400)
        {
            await Task.Delay(20, cancellationToken);
            waited += 20;
        }
        sw.Stop();

        if (!exchange.IsComplete || !exchange.IsSuccess)
        {
            var unavailSample = new LiveDataSample
            {
                ParameterId = def.Id,
                Name = def.Name,
                Category = def.Category,
                Unit = def.Unit,
                TimestampUtc = DateTime.UtcNow,
                Quality = DataQuality.Unavailable,
                QualityReason = exchange.Summary,
                Source = def.Source,
                EcuName = ecuName
            };
            IngestSample(unavailSample);
            return unavailSample;
        }

        // Parse Response
        byte[] respBytes = Array.Empty<byte>();
        if (!string.IsNullOrEmpty(exchange.ResponseDataHex))
        {
            string[] hexParts = exchange.ResponseDataHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            respBytes = new byte[hexParts.Length];
            for (int i = 0; i < hexParts.Length; i++)
            {
                byte.TryParse(hexParts[i], System.Globalization.NumberStyles.HexNumber, null, out respBytes[i]);
            }
        }

        double val = 0.0;
        byte[] dataSlice = Array.Empty<byte>();

        if (def.Source == LiveDataProtocolSource.UdsDid && respBytes.Length >= 3 && respBytes[0] == 0x62)
        {
            dataSlice = respBytes[3..];
            val = DecodeNumericFromBytes(def.Id, dataSlice);
        }
        else if (respBytes.Length >= 2 && respBytes[0] == 0x41)
        {
            dataSlice = respBytes[2..];
            val = DecodeObdPidNumeric(def.ObdPid ?? 0, dataSlice);
        }

        var sample = new LiveDataSample
        {
            ParameterId = def.Id,
            Name = def.Name,
            Category = def.Category,
            RawBytes = dataSlice,
            RawValue = val,
            ScaledValue = (val * def.Scaling) + def.Offset,
            FormattedValue = $"{((val * def.Scaling) + def.Offset):F1} {def.Unit}".Trim(),
            Unit = def.Unit,
            TimestampUtc = DateTime.UtcNow,
            Quality = DataQuality.Valid,
            Source = def.Source,
            EcuName = ecuName
        };

        IngestSample(sample);
        return sample;
    }

    public void StartPolling(uint requestCanId, IEnumerable<string> parameterIds, int intervalMs = 250, string ecuName = "ECM")
    {
        StopPolling();

        var paramList = parameterIds.Where(id => _definitions.ContainsKey(id)).ToList();
        if (paramList.Count == 0) paramList = _definitions.Keys.Take(6).ToList();

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _pollingTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var pId in paramList)
                    {
                        if (token.IsCancellationRequested) break;
                        await PollParameterAsync(requestCanId, pId, ecuName, token);
                        await Task.Delay(25, token); // Inter-poll delay
                    }

                    await Task.Delay(intervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, token);
                }
            }
        }, token);
    }

    public void StopPolling()
    {
        if (_pollingCts != null)
        {
            _pollingCts.Cancel();
            _pollingCts.Dispose();
            _pollingCts = null;
        }
        _pollingTask = null;
    }

    private double DecodeNumericFromBytes(string paramId, byte[] data)
    {
        if (data == null || data.Length == 0) return 0.0;

        return paramId switch
        {
            "ENGINE_RPM" when data.Length >= 2 => (data[0] << 8) | data[1],
            "COOLANT_TEMP" when data.Length >= 1 => data[0] - 40,
            "MODULE_VOLTAGE" when data.Length >= 2 => ((data[0] << 8) | data[1]) / 1000.0,
            "THROTTLE_POS" when data.Length >= 1 => data[0] * 100.0 / 255.0,
            "MASS_AIR_FLOW" when data.Length >= 2 => ((data[0] << 8) | data[1]) / 100.0,
            "MANIFOLD_PRESSURE" when data.Length >= 1 => data[0],
            "LAMBDA" when data.Length >= 2 => ((data[0] << 8) | data[1]) / 1000.0,
            "STFT" when data.Length >= 1 => (data[0] - 128) * 100.0 / 128.0,
            "LTFT" when data.Length >= 1 => (data[0] - 128) * 100.0 / 128.0,
            "VEHICLE_SPEED" when data.Length >= 1 => data[0],
            "INTAKE_AIR_TEMP" when data.Length >= 1 => data[0] - 40,
            _ when data.Length == 1 => data[0],
            _ when data.Length >= 2 => (data[0] << 8) | data[1],
            _ => 0.0
        };
    }

    private double DecodeObdPidNumeric(byte pid, byte[] data)
    {
        if (data == null || data.Length == 0) return 0.0;

        return pid switch
        {
            0x0C when data.Length >= 2 => ((data[0] * 256.0) + data[1]) / 4.0, // RPM
            0x0D when data.Length >= 1 => data[0], // Speed km/h
            0x05 when data.Length >= 1 => data[0] - 40.0, // Coolant Temp
            0x0F when data.Length >= 1 => data[0] - 40.0, // IAT
            0x10 when data.Length >= 2 => ((data[0] * 256.0) + data[1]) / 100.0, // MAF
            0x11 when data.Length >= 1 => (data[0] * 100.0) / 255.0, // Throttle
            0x42 when data.Length >= 2 => ((data[0] * 256.0) + data[1]) / 1000.0, // Control module voltage
            0x06 when data.Length >= 1 => (data[0] - 128.0) * 100.0 / 128.0, // STFT
            0x07 when data.Length >= 1 => (data[0] - 128.0) * 100.0 / 128.0, // LTFT
            0x04 when data.Length >= 1 => (data[0] * 100.0) / 255.0, // Engine Load
            _ => data[0]
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopPolling();
    }
}
