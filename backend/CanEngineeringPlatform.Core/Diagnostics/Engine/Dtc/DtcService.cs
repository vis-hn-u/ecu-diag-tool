using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;

public interface IDtcService
{
    Task<IReadOnlyList<DtcRecord>> ReadDtcsByStatusMaskAsync(uint requestCanId, byte statusMask = 0xFF, string ecuName = "ECM", CancellationToken cancellationToken = default);
    Task<DtcClearResult> ClearDtcsWithSafetyAsync(DtcClearRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<DtcRecord> GetActiveDtcs(string? ecuName = null);
    IReadOnlyList<DtcRecord> GetHistoricalDtcs(string? ecuName = null);
    IReadOnlyList<DtcClearResult> GetClearHistory(string? ecuName = null);
    event Action<IReadOnlyList<DtcRecord>>? OnDtcsChanged;
    event Action<DtcClearResult>? OnDtcCleared;
}

public class DtcService : IDtcService
{
    private readonly IDiagnosticSessionService _sessionService;
    private readonly IDiagnosticSessionManager _sessionManager;
    private readonly IDiagnosticTraceService _traceService;

    private readonly ConcurrentDictionary<string, DtcRecord> _activeDtcs = new();
    private readonly List<DtcRecord> _historicalDtcs = new();
    private readonly List<DtcClearResult> _clearHistory = new();
    private readonly object _historyLock = new();

    public event Action<IReadOnlyList<DtcRecord>>? OnDtcsChanged;
    public event Action<DtcClearResult>? OnDtcCleared;

    public DtcService(
        IDiagnosticSessionService sessionService,
        IDiagnosticSessionManager sessionManager,
        IDiagnosticTraceService traceService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
    }

    public async Task<IReadOnlyList<DtcRecord>> ReadDtcsByStatusMaskAsync(
        uint requestCanId,
        byte statusMask = 0xFF,
        string ecuName = "ECM",
        CancellationToken cancellationToken = default)
    {
        // ISO 14229-1 UDS Service 0x19 Subfunction 0x02 (ReportDTCByStatusMask)
        byte[] requestPayload = new byte[] { (byte)UdsServiceId.ReadDTCInformation, 0x02, statusMask };

        var sw = Stopwatch.StartNew();
        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = requestCanId,
            ResponseCanId = requestCanId + 8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.ReadDTCInformation,
            SubFunction = 0x02,
            Payload = requestPayload,
            Result = DiagnosticExchangeResult.Pending,
            ActiveSession = _sessionManager.GetStatus().CurrentState
        });

        var exchange = await _sessionService.SendRequestAsync(requestCanId, requestPayload, ecuName, isSimulation: true);

        int waitTime = 0;
        while (!exchange.IsComplete && waitTime < 600)
        {
            await Task.Delay(20, cancellationToken);
            waitTime += 20;
        }
        sw.Stop();

        if (!exchange.IsComplete || !exchange.IsSuccess)
        {
            _traceService.RecordTrace(new DiagnosticTraceEntry
            {
                TransportId = "iso-tp",
                RequestCanId = requestCanId,
                ResponseCanId = requestCanId + 8,
                Direction = DiagnosticDirection.Rx,
                Protocol = DiagnosticProtocolType.Uds,
                ServiceId = (byte)UdsServiceId.ReadDTCInformation,
                SubFunction = 0x02,
                Payload = Array.Empty<byte>(),
                Result = exchange.IsComplete ? DiagnosticExchangeResult.NegativeResponse : DiagnosticExchangeResult.Timeout,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                ActiveSession = _sessionManager.GetStatus().CurrentState,
                Notes = exchange.Summary
            });

            return GetActiveDtcs(ecuName);
        }

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

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = requestCanId,
            ResponseCanId = requestCanId + 8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.ReadDTCInformation,
            SubFunction = 0x02,
            Payload = respBytes,
            Result = DiagnosticExchangeResult.PositiveResponse,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            ActiveSession = _sessionManager.GetStatus().CurrentState
        });

        var parsedDtcs = Parse0x1902Payload(respBytes, ecuName, requestCanId + 8);

        // Update active dictionary
        // Remove old DTCs for this ECU that are no longer reported
        var currentEcuKeys = _activeDtcs.Values
            .Where(d => d.EcuName.Equals(ecuName, StringComparison.OrdinalIgnoreCase))
            .Select(d => $"{d.EcuName}_{d.Code}")
            .ToList();

        var newKeys = new HashSet<string>(parsedDtcs.Select(d => $"{d.EcuName}_{d.Code}"));

        foreach (var key in currentEcuKeys)
        {
            if (!newKeys.Contains(key))
            {
                _activeDtcs.TryRemove(key, out _);
            }
        }

        foreach (var dtc in parsedDtcs)
        {
            string key = $"{dtc.EcuName}_{dtc.Code}";
            _activeDtcs[key] = dtc;

            lock (_historyLock)
            {
                _historicalDtcs.Add(dtc);
            }
        }

        OnDtcsChanged?.Invoke(GetActiveDtcs());
        return parsedDtcs;
    }

    public async Task<DtcClearResult> ClearDtcsWithSafetyAsync(DtcClearRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.RequireUserConfirmation && !request.UserConfirmed)
        {
            throw new UnauthorizedAccessException("Operator confirmation is strictly required before clearing diagnostic information.");
        }

        // 1. Snapshot Pre-Clear DTCs
        var preClearDtcs = GetActiveDtcs(request.EcuName);

        // 2. Formulate UDS 0x14 ClearDiagnosticInformation request
        // 0x14 <groupOfDTC_high> <groupOfDTC_mid> <groupOfDTC_low>
        byte gHigh = (byte)((request.GroupOfDtc >> 16) & 0xFF);
        byte gMid = (byte)((request.GroupOfDtc >> 8) & 0xFF);
        byte gLow = (byte)(request.GroupOfDtc & 0xFF);
        byte[] clearPayload = new byte[] { (byte)UdsServiceId.ClearDiagnosticInformation, gHigh, gMid, gLow };

        var sw = Stopwatch.StartNew();
        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.ClearDiagnosticInformation,
            Payload = clearPayload,
            Result = DiagnosticExchangeResult.Pending,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = $"Clear DTC Request for ECU {request.EcuName} (Group 0x{request.GroupOfDtc:X6})"
        });

        var exchange = await _sessionService.SendRequestAsync(request.RequestCanId, clearPayload, request.EcuName, isSimulation: true);

        int waitTime = 0;
        while (!exchange.IsComplete && waitTime < 600)
        {
            await Task.Delay(20, cancellationToken);
            waitTime += 20;
        }
        sw.Stop();

        bool success = exchange.IsComplete && exchange.IsSuccess;
        string? error = success ? null : exchange.Summary;

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.ClearDiagnosticInformation,
            Payload = success ? new byte[] { 0x54 } : new byte[] { 0x7F, 0x14, 0x22 },
            Result = success ? DiagnosticExchangeResult.PositiveResponse : DiagnosticExchangeResult.NegativeResponse,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = success ? "Positive response (0x54) received for ClearDiagnosticInformation." : error
        });

        if (!success)
        {
            var failedResult = new DtcClearResult
            {
                EcuName = request.EcuName,
                ClearedUtc = DateTime.UtcNow,
                Success = false,
                ErrorMessage = error ?? "ClearDiagnosticInformation timed out or was rejected.",
                PreClearDtcs = preClearDtcs,
                PostClearDtcs = preClearDtcs,
                ClearedCodes = Array.Empty<string>(),
                ReturningCodes = preClearDtcs.Select(d => d.Code).ToList()
            };

            lock (_historyLock)
            {
                _clearHistory.Add(failedResult);
            }
            OnDtcCleared?.Invoke(failedResult);
            return failedResult;
        }

        // 3. Post-Clear Verification: Re-read DTCs from ECU
        await Task.Delay(100, cancellationToken); // Settle time
        var postClearDtcs = await ReadDtcsByStatusMaskAsync(request.RequestCanId, 0xFF, request.EcuName, cancellationToken);

        var preCodes = new HashSet<string>(preClearDtcs.Select(d => d.Code));
        var postCodes = new HashSet<string>(postClearDtcs.Select(d => d.Code));

        var clearedList = preCodes.Where(c => !postCodes.Contains(c)).ToList();
        var returningList = preCodes.Where(c => postCodes.Contains(c)).ToList();

        var result = new DtcClearResult
        {
            EcuName = request.EcuName,
            ClearedUtc = DateTime.UtcNow,
            Success = true,
            PreClearDtcs = preClearDtcs,
            PostClearDtcs = postClearDtcs,
            ClearedCodes = clearedList,
            ReturningCodes = returningList
        };

        lock (_historyLock)
        {
            _clearHistory.Add(result);
        }

        OnDtcCleared?.Invoke(result);
        return result;
    }

    public IReadOnlyList<DtcRecord> GetActiveDtcs(string? ecuName = null)
    {
        var query = _activeDtcs.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(ecuName))
        {
            query = query.Where(d => d.EcuName.Equals(ecuName, StringComparison.OrdinalIgnoreCase));
        }
        return query.OrderBy(d => d.Code).ToList();
    }

    public IReadOnlyList<DtcRecord> GetHistoricalDtcs(string? ecuName = null)
    {
        lock (_historyLock)
        {
            var query = _historicalDtcs.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(ecuName))
            {
                query = query.Where(d => d.EcuName.Equals(ecuName, StringComparison.OrdinalIgnoreCase));
            }
            return query.OrderByDescending(d => d.DetectedUtc).ToList();
        }
    }

    public IReadOnlyList<DtcClearResult> GetClearHistory(string? ecuName = null)
    {
        lock (_historyLock)
        {
            var query = _clearHistory.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(ecuName))
            {
                query = query.Where(h => h.EcuName.Equals(ecuName, StringComparison.OrdinalIgnoreCase));
            }
            return query.OrderByDescending(h => h.ClearedUtc).ToList();
        }
    }

    private List<DtcRecord> Parse0x1902Payload(byte[] payload, string ecuName, uint canId)
    {
        var list = new List<DtcRecord>();
        if (payload == null || payload.Length < 3) return list;

        // Response format: 0x59 0x02 <DTCStatusAvailabilityMask> [DTC_H DTC_M DTC_L Status]...
        if (payload[0] != 0x59 || payload[1] != 0x02) return list;

        int index = 3;
        while (index + 4 <= payload.Length)
        {
            byte b0 = payload[index];
            byte b1 = payload[index + 1];
            byte b2 = payload[index + 2];
            byte statusByte = payload[index + 3];

            uint rawDtc = (uint)((b0 << 16) | (b1 << 8) | b2);
            string formattedCode = DtcDecoder.Decode3ByteDtc(b0, b1, b2);
            string description = DtcDecoder.GetDescription(formattedCode);
            if (string.IsNullOrEmpty(description) && formattedCode.Contains("-"))
            {
                description = DtcDecoder.GetDescription(formattedCode.Split('-')[0]);
            }

            list.Add(new DtcRecord
            {
                Code = formattedCode,
                RawDtcNumber = rawDtc,
                StatusByte = statusByte,
                Description = string.IsNullOrEmpty(description) ? "Unspecified DTC Diagnostic Definition" : description,
                DescriptionSource = "SAE J2012 / OEM Diagnostic Catalog",
                EcuName = ecuName,
                CanId = canId,
                DetectedUtc = DateTime.UtcNow,
                SessionWhenRead = _sessionManager.GetStatus().CurrentState,
                RawResponseBytes = payload[index..(index + 4)]
            });

            index += 4;
        }

        return list;
    }
}
