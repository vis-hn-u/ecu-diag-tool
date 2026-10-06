using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.EcuEngineering.Protocols;

public interface IProtocolDiscoveryService
{
    Task<EcuProtocolCapabilityMatrix> DiscoverProtocolAsync(uint requestCanId = 0x7E0, uint responseCanId = 0x7E8, string ecuName = "ECM");
    IReadOnlyList<EcuProtocolCapabilityMatrix> GetDiscoveredMatrices();
    EcuProtocolCapabilityMatrix? GetMatrixForEcu(string ecuName);
}

public class ProtocolDiscoveryService : IProtocolDiscoveryService
{
    private readonly IDiagnosticSessionService _diagnosticSession;
    private readonly Dictionary<string, EcuProtocolCapabilityMatrix> _cachedMatrices = new(StringComparer.OrdinalIgnoreCase);

    public ProtocolDiscoveryService(IDiagnosticSessionService diagnosticSession)
    {
        _diagnosticSession = diagnosticSession ?? throw new ArgumentNullException(nameof(diagnosticSession));
    }

    public async Task<EcuProtocolCapabilityMatrix> DiscoverProtocolAsync(uint requestCanId = 0x7E0, uint responseCanId = 0x7E8, string ecuName = "ECM")
    {
        var matrix = new EcuProtocolCapabilityMatrix
        {
            EcuName = ecuName,
            RequestCanId = requestCanId,
            ResponseCanId = responseCanId,
            DiscoveryTimestampUtc = DateTime.UtcNow
        };

        // Populate baseline known UDS services
        InitializeServiceMatrix(matrix);

        // Step 1: Probe Standard 11-bit UDS via TesterPresent (0x3E 0x00)
        try
        {
            var udsProbe = await SendAndWaitAsync(requestCanId, new byte[] { 0x3E, 0x00 }, ecuName);
            if (udsProbe.IsComplete && (udsProbe.IsSuccess || udsProbe.ResponseDataHex?.Contains("7E") == true || udsProbe.ResponseDataHex?.Contains("7F") == true))
            {
                matrix.PrimaryProtocol = DiagnosticProtocolType.UdsOnIsoTp;
                matrix.AddressingScheme = AddressingSchemeType.Standard11BitUnicast;
                matrix.SupportsIsoTp = true;
                matrix.Services[0x3E].IsSupported = true;
                matrix.Services[0x3E].Notes = "Responded to TesterPresent";

                // Check ReadDataByIdentifier (0x22)
                var didProbe = await SendAndWaitAsync(requestCanId, new byte[] { 0x22, 0xF1, 0x90 }, ecuName);
                if (didProbe.IsComplete && (didProbe.IsSuccess || didProbe.ResponseDataHex?.StartsWith("62") == true))
                {
                    matrix.Services[0x22].IsSupported = true;
                    matrix.Services[0x22].Notes = "Active DID Read Supported";
                }

                // Check ReadDTC (0x19)
                var dtcProbe = await SendAndWaitAsync(requestCanId, new byte[] { 0x19, 0x02, 0xFF }, ecuName);
                if (dtcProbe.IsComplete && (dtcProbe.IsSuccess || dtcProbe.ResponseDataHex?.StartsWith("59") == true))
                {
                    matrix.Services[0x19].IsSupported = true;
                    matrix.Services[0x19].Notes = "DTC Subfunction 0x02 supported";
                }

                _cachedMatrices[ecuName] = matrix;
                return matrix;
            }
        }
        catch
        {
            // Fall through to OBD-II probe
        }

        // Step 2: Probe OBD-II Mode 1 PID 0 (0x01 0x00)
        try
        {
            var obdProbe = await SendAndWaitAsync(requestCanId, new byte[] { 0x01, 0x00 }, ecuName);
            if (obdProbe.IsComplete && (obdProbe.IsSuccess || obdProbe.ResponseDataHex?.StartsWith("41") == true))
            {
                matrix.PrimaryProtocol = DiagnosticProtocolType.Obd2Iso15765;
                matrix.AddressingScheme = AddressingSchemeType.Standard11BitUnicast;
                matrix.SupportsIsoTp = true;
                matrix.Services[0x01] = new ServiceCapability { ServiceId = 0x01, ServiceName = "OBD-II Current Powertrain Diagnostic Data", IsSupported = true, Notes = "Responded to PID 00" };
                matrix.Services[0x09] = new ServiceCapability { ServiceId = 0x09, ServiceName = "OBD-II Vehicle Info (VIN / CALID)", IsSupported = true, Notes = "Supported" };
                _cachedMatrices[ecuName] = matrix;
                return matrix;
            }
        }
        catch
        {
            // Fall through
        }

        // When no diagnostic responder is present on this CAN ID
        matrix.PrimaryProtocol = DiagnosticProtocolType.Unknown;
        matrix.AddressingScheme = AddressingSchemeType.Unknown;
        matrix.SupportsIsoTp = false;
        _cachedMatrices[ecuName] = matrix;
        return matrix;
    }

    private async Task<DiagnosticExchange> SendAndWaitAsync(uint canId, byte[] payload, string ecuName)
    {
        var exchange = await _diagnosticSession.SendRequestAsync(canId, payload, ecuName, isSimulation: true);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!exchange.IsComplete && sw.ElapsedMilliseconds < 250)
        {
            await Task.Delay(10);
        }
        return exchange;
    }

    public IReadOnlyList<EcuProtocolCapabilityMatrix> GetDiscoveredMatrices()
    {
        return _cachedMatrices.Values.ToList();
    }

    public EcuProtocolCapabilityMatrix? GetMatrixForEcu(string ecuName)
    {
        if (string.IsNullOrWhiteSpace(ecuName)) return null;
        _cachedMatrices.TryGetValue(ecuName, out var matrix);
        return matrix;
    }

    private static void InitializeServiceMatrix(EcuProtocolCapabilityMatrix matrix)
    {
        matrix.Services[0x10] = new ServiceCapability { ServiceId = 0x10, ServiceName = "DiagnosticSessionControl", IsSupported = true, RequiresSecurityAccess = false };
        matrix.Services[0x11] = new ServiceCapability { ServiceId = 0x11, ServiceName = "ECUReset", IsSupported = true, RequiresSecurityAccess = false };
        matrix.Services[0x14] = new ServiceCapability { ServiceId = 0x14, ServiceName = "ClearDiagnosticInformation", IsSupported = true, RequiresSecurityAccess = false };
        matrix.Services[0x19] = new ServiceCapability { ServiceId = 0x19, ServiceName = "ReadDTCInformation", IsSupported = true, RequiresSecurityAccess = false };
        matrix.Services[0x22] = new ServiceCapability { ServiceId = 0x22, ServiceName = "ReadDataByIdentifier", IsSupported = true, RequiresSecurityAccess = false };
        matrix.Services[0x23] = new ServiceCapability { ServiceId = 0x23, ServiceName = "ReadMemoryByAddress", IsSupported = false, RequiresSecurityAccess = true, Notes = "Protected memory reading" };
        matrix.Services[0x27] = new ServiceCapability { ServiceId = 0x27, ServiceName = "SecurityAccess", IsSupported = true, RequiresSecurityAccess = false, Notes = "Seed/Key challenge handshake" };
        matrix.Services[0x2E] = new ServiceCapability { ServiceId = 0x2E, ServiceName = "WriteDataByIdentifier", IsSupported = false, RequiresSecurityAccess = true, Notes = "Calibration / Configuration write" };
        matrix.Services[0x31] = new ServiceCapability { ServiceId = 0x31, ServiceName = "RoutineControl", IsSupported = true, RequiresSecurityAccess = true };
        matrix.Services[0x34] = new ServiceCapability { ServiceId = 0x34, ServiceName = "RequestDownload", IsSupported = false, RequiresSecurityAccess = true, Notes = "Flash programming service" };
        matrix.Services[0x36] = new ServiceCapability { ServiceId = 0x36, ServiceName = "TransferData", IsSupported = false, RequiresSecurityAccess = true, Notes = "Block flash transmission" };
        matrix.Services[0x37] = new ServiceCapability { ServiceId = 0x37, ServiceName = "RequestTransferExit", IsSupported = false, RequiresSecurityAccess = true, Notes = "Flash sequence finalize" };
        matrix.Services[0x3E] = new ServiceCapability { ServiceId = 0x3E, ServiceName = "TesterPresent", IsSupported = true, RequiresSecurityAccess = false, Notes = "Keepalive beacon" };
    }
}
