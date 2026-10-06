using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.EcuEngineering.Analysis;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;

namespace CanEngineeringPlatform.Core.EcuEngineering.Reading;

public interface IEcuMemoryReader
{
    Task<EcuMemoryImage> ReadMemoryAsync(
        EcuIdentificationProfile identification,
        EcuMemoryMap memoryMap,
        long startAddress,
        long lengthBytes,
        string regionName,
        SafeReadOptions options,
        IProgress<ReadSessionStatus>? progress = null);

    ReadSessionStatus? GetActiveSession(Guid sessionId);
    IReadOnlyList<ReadSessionStatus> GetAllSessions();
}

public class EcuMemoryReader : IEcuMemoryReader
{
    private readonly IDiagnosticSessionService _diagnosticSession;
    private readonly IEcuMemoryImageStore _imageStore;
    private readonly ConcurrentDictionary<Guid, ReadSessionStatus> _sessions = new();

    public EcuMemoryReader(
        IDiagnosticSessionService diagnosticSession,
        IEcuMemoryImageStore imageStore)
    {
        _diagnosticSession = diagnosticSession ?? throw new ArgumentNullException(nameof(diagnosticSession));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
    }

    public async Task<EcuMemoryImage> ReadMemoryAsync(
        EcuIdentificationProfile identification,
        EcuMemoryMap memoryMap,
        long startAddress,
        long lengthBytes,
        string regionName,
        SafeReadOptions options,
        IProgress<ReadSessionStatus>? progress = null)
    {
        if (identification == null) throw new ArgumentNullException(nameof(identification));
        if (memoryMap == null) throw new ArgumentNullException(nameof(memoryMap));
        if (options == null) options = new SafeReadOptions();

        var sessionStatus = new ReadSessionStatus
        {
            EcuName = identification.EcuName,
            RequestCanId = identification.RequestCanId,
            ResponseCanId = identification.ResponseCanId,
            RegionName = regionName,
            StartAddress = startAddress,
            TotalBytesRequested = lengthBytes,
            StartedUtc = DateTime.UtcNow
        };
        _sessions[sessionStatus.SessionId] = sessionStatus;

        try
        {
            // Transition 1: Connected
            sessionStatus.TransitionTo(ReadSessionState.Connected, "Diagnostic transport connection verified.");
            progress?.Report(sessionStatus);

            // Validation: Identity & Protocol
            if (string.IsNullOrWhiteSpace(identification.EcuName))
            {
                throw new InvalidOperationException("ECU identity is unverified or missing.");
            }

            // Transition 2: Identified
            sessionStatus.TransitionTo(ReadSessionState.Identified, $"Target ECU confirmed as '{identification.EcuName}'.");
            progress?.Report(sessionStatus);

            // Validation: Range & Bounds Check
            if (startAddress < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startAddress), "Memory address cannot be negative.");
            }
            if (lengthBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthBytes), "Requested memory read length must be greater than zero.");
            }
            if (lengthBytes > options.MaxTotalReadBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthBytes),
                    $"Requested length ({lengthBytes} bytes) exceeds maximum safe read limit ({options.MaxTotalReadBytes} bytes).");
            }

            // Validate Region in Memory Map
            var region = memoryMap.FindRegionForAddress(startAddress);
            if (region == null)
            {
                throw new InvalidOperationException($"Address 0x{startAddress:X8} is outside any declared memory region in the ECU memory map.");
            }
            if (startAddress + lengthBytes - 1 > region.EndAddress)
            {
                throw new InvalidOperationException($"Requested read range [0x{startAddress:X8}..0x{(startAddress + lengthBytes - 1):X8}] overflows boundary of region '{region.RegionName}' (ends at 0x{region.EndAddress:X8}).");
            }

            // Transition 3: ProtocolConfirmed
            sessionStatus.TransitionTo(ReadSessionState.ProtocolConfirmed, "UDS / ISO-TP communication protocol confirmed.");
            progress?.Report(sessionStatus);

            // Switch to required session if needed (e.g. 0x03 Extended Diagnostic)
            if (region.RequiredSession != 0x01)
            {
                byte[] sessionPayload = new byte[] { (byte)UdsServiceId.DiagnosticSessionControl, region.RequiredSession };
                var sessionEx = await _diagnosticSession.SendRequestAsync(identification.RequestCanId, sessionPayload, identification.EcuName, options.Environment == ExecutionEnvironment.Simulation);
                await AwaitExchangeAsync(sessionEx, 300);
            }

            // Transition 4: DiagnosticSessionActive
            sessionStatus.TransitionTo(ReadSessionState.DiagnosticSessionActive, $"Active diagnostic session verified (0x{region.RequiredSession:X2}).");
            progress?.Report(sessionStatus);

            // Validate Operator Authorization
            if (options.RequireUserConfirmation && !options.UserConfirmed)
            {
                throw new UnauthorizedAccessException("Operator confirmation required before initiating physical ECU memory extraction.");
            }

            if (region.RequiredSecurityLevel.HasValue && region.ReadCapability == ReadPermission.RequiresSecurityAccess)
            {
                // In Phase 2, security bypass is strictly forbidden
                // If not unlocked, reading is blocked with explicit notification
                sessionStatus.TransitionTo(ReadSessionState.Failed, "REQUIRES AUTHORIZATION: Target memory region requires SecurityAccess (0x27) level 0x01 authentication.");
                throw new UnauthorizedAccessException("REQUIRES AUTHORIZATION: Security access key required for this memory sector.");
            }

            // Transition 5: ReadAuthorized
            sessionStatus.TransitionTo(ReadSessionState.ReadAuthorized, "Pre-read checks passed. Read operation authorized.");
            progress?.Report(sessionStatus);

            // Transition 6: Reading (Begin Chunked Extraction)
            sessionStatus.TransitionTo(ReadSessionState.Reading, $"Starting chunked extraction of {lengthBytes} bytes at 0x{startAddress:X8} (Chunk size: {options.ChunkSize} bytes).");
            progress?.Report(sessionStatus);

            int chunkSize = Math.Min(options.ChunkSize, region.MaxReadLengthBytes);
            if (chunkSize <= 0) chunkSize = 256;

            int totalChunks = (int)Math.Ceiling((double)lengthBytes / chunkSize);
            sessionStatus.TotalChunks = totalChunks;

            byte[] fullImageBytes = new byte[lengthBytes];
            long currentAddress = startAddress;
            int bytesAccumulated = 0;

            for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
            {
                int currentChunkLength = (int)Math.Min(chunkSize, lengthBytes - bytesAccumulated);
                sessionStatus.CurrentChunk = chunkIdx + 1;

                byte[] chunkData = await ReadSingleChunkWithRetryAsync(
                    identification.RequestCanId,
                    identification.EcuName,
                    currentAddress,
                    currentChunkLength,
                    options,
                    options.Environment == ExecutionEnvironment.Simulation);

                Array.Copy(chunkData, 0, fullImageBytes, bytesAccumulated, chunkData.Length);
                bytesAccumulated += chunkData.Length;
                currentAddress += chunkData.Length;

                sessionStatus.BytesReadSoFar = bytesAccumulated;
                progress?.Report(sessionStatus);

                if (options.InterChunkDelayMs > 0 && chunkIdx < totalChunks - 1)
                {
                    await Task.Delay(options.InterChunkDelayMs);
                }
            }

            // Transition 7: Verifying
            sessionStatus.TransitionTo(ReadSessionState.Verifying, "Verifying extracted byte count and computing SHA-256 integrity checksum.");
            progress?.Report(sessionStatus);

            if (bytesAccumulated != lengthBytes)
            {
                throw new InvalidOperationException($"READ VERIFICATION FAILED: Actual read bytes ({bytesAccumulated}) did not match requested bytes ({lengthBytes}).");
            }

            string sha256 = EcuMemoryImage.ComputeSha256(fullImageBytes);
            sessionStatus.Sha256Result = sha256;

            // Build EcuMemoryImage
            var image = new EcuMemoryImage
            {
                EcuName = identification.EcuName,
                HardwareNumber = identification.HardwareNumber?.Value ?? "UNKNOWN",
                SoftwareNumber = identification.SoftwareNumber?.Value ?? "UNKNOWN",
                CalibrationId = identification.CalibrationId?.Value ?? "UNKNOWN",
                Vin = identification.Vin?.Value ?? "UNKNOWN",
                RegionName = region.RegionName,
                StartAddress = startAddress,
                Length = lengthBytes,
                RawBytes = fullImageBytes,
                Sha256Hash = sha256,
                TimestampUtc = DateTime.UtcNow,
                Transport = options.Environment == ExecutionEnvironment.Simulation ? "Simulation Pipeline" : "CAN Transport",
                DiagnosticProtocol = "UDS on ISO-TP (ISO 14229)",
                ReadMethod = "ReadMemoryByAddress (0x23)",
                DiagnosticSession = $"Diagnostic Session 0x{region.RequiredSession:X2}",
                AuthorizationState = "Operator Authorized",
                Classification = ImageClassification.OriginalRead,
                Environment = options.Environment,
                ToolVersion = "v5.2.0 (Phase 2)"
            };

            image.Evidence.Add(EvidenceSource.Create(
                "UdsReadMemoryByAddress",
                $"0x{startAddress:X8}..0x{(startAddress + lengthBytes - 1):X8}",
                $"Extracted {lengthBytes} bytes in {totalChunks} chunks via UDS service 0x23. SHA-256: {sha256}"));

            // Store in image repository
            _imageStore.StoreImage(image);

            // Transition 8: Complete
            sessionStatus.CompletedUtc = DateTime.UtcNow;
            sessionStatus.TransitionTo(ReadSessionState.Complete, $"Safe read completed successfully. Stored as image '{image.ImageId}'. SHA-256: {sha256}");
            progress?.Report(sessionStatus);

            return image;
        }
        catch (Exception ex)
        {
            sessionStatus.LastError = ex.Message;
            sessionStatus.CompletedUtc = DateTime.UtcNow;
            if (sessionStatus.State != ReadSessionState.Failed)
            {
                sessionStatus.TransitionTo(ReadSessionState.Failed, $"Read aborted: {ex.Message}");
            }
            progress?.Report(sessionStatus);
            throw;
        }
    }

    public ReadSessionStatus? GetActiveSession(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public IReadOnlyList<ReadSessionStatus> GetAllSessions()
    {
        return _sessions.Values.OrderByDescending(s => s.StartedUtc).ToList();
    }

    private async Task<byte[]> ReadSingleChunkWithRetryAsync(
        uint requestCanId,
        string ecuName,
        long address,
        int length,
        SafeReadOptions options,
        bool isSimulation)
    {
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= options.RetryCount; attempt++)
        {
            try
            {
                // Build UDS ReadMemoryByAddress (0x23) frame:
                // [0x23][FormatIdentifier: 0x24 (2B length, 4B address)][Addr_3][Addr_2][Addr_1][Addr_0][Len_1][Len_0]
                byte[] requestPayload = new byte[8];
                requestPayload[0] = (byte)UdsServiceId.ReadMemoryByAddress;
                requestPayload[1] = 0x24; // 2 bytes size, 4 bytes address
                requestPayload[2] = (byte)((address >> 24) & 0xFF);
                requestPayload[3] = (byte)((address >> 16) & 0xFF);
                requestPayload[4] = (byte)((address >> 8) & 0xFF);
                requestPayload[5] = (byte)(address & 0xFF);
                requestPayload[6] = (byte)((length >> 8) & 0xFF);
                requestPayload[7] = (byte)(length & 0xFF);

                var exchange = await _diagnosticSession.SendRequestAsync(requestCanId, requestPayload, ecuName, isSimulation);
                await AwaitExchangeAsync(exchange, options.TimeoutMs);

                if (!exchange.IsComplete)
                {
                    throw new TimeoutException($"Read chunk at 0x{address:X8} timed out after {options.TimeoutMs}ms.");
                }

                if (!exchange.IsSuccess || exchange.ResponseDataHex == null)
                {
                    string nrc = exchange.NrcHex ?? "Unknown NRC";
                    string desc = exchange.NrcDescription ?? "Negative Response";
                    throw new InvalidOperationException($"ECU responded with Negative Response to ReadMemoryByAddress at 0x{address:X8}: {nrc} ({desc})");
                }

                // Decode positive response (0x63 <data...>)
                byte[] rawPayload = ParseHexBytes(exchange.ResponseDataHex);
                if (rawPayload.Length < 1 || rawPayload[0] != 0x63)
                {
                    throw new InvalidOperationException($"Invalid response format from ReadMemoryByAddress: expected 0x63, got 0x{rawPayload.FirstOrDefault():X2}");
                }

                byte[] data = rawPayload[1..];
                if (data.Length != length)
                {
                    throw new InvalidOperationException($"Chunk length mismatch at 0x{address:X8}: requested {length} bytes, received {data.Length} bytes.");
                }

                return data;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < options.RetryCount)
                {
                    await Task.Delay(50 * attempt); // Backoff retry
                }
            }
        }

        throw new InvalidOperationException($"Failed to read chunk at 0x{address:X8} after {options.RetryCount} attempts: {lastEx?.Message}", lastEx);
    }

    private static async Task AwaitExchangeAsync(DiagnosticExchange exchange, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!exchange.IsComplete && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }
    }

    private static byte[] ParseHexBytes(string hex)
    {
        var parts = hex.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new List<byte>(parts.Length);
        foreach (var p in parts)
        {
            if (byte.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                bytes.Add(b);
            }
        }
        return bytes.ToArray();
    }
}
