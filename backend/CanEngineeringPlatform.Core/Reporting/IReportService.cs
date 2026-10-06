using System.Threading.Tasks;

namespace CanEngineeringPlatform.Core.Reporting;

public interface IReportService
{
    Task<byte[]> GenerateDiagnosticReportPdfAsync(string? vehicleId = null, string? notes = null);
    Task<byte[]> GenerateDynoReportPdfAsync(string? pullId = null, string? baselinePullId = null, string? vehicleId = null);
    Task<byte[]> GenerateEcuFlashReportPdfAsync(string? fileHash = null, string? packId = null, string? vehicleId = null);
    Task<byte[]> GenerateSessionLogReportPdfAsync(string? sessionId = null, string? vehicleId = null);
    Task<byte[]> GenerateVehicleHealthReportPdfAsync(string? vehicleId = null, string? notes = null);

    ReportDefinition BuildDiagnosticReport(string? vehicleId = null, string? notes = null);
    ReportDefinition BuildDynoReport(string? pullId = null, string? baselinePullId = null, string? vehicleId = null);
    ReportDefinition BuildEcuFlashReport(string? fileHash = null, string? packId = null, string? vehicleId = null);
    ReportDefinition BuildSessionLogReport(string? sessionId = null, string? vehicleId = null);
    ReportDefinition BuildVehicleHealthReport(string? vehicleId = null, string? notes = null);
}
