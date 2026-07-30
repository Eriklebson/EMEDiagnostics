namespace EME.Diagnostics.Networking.Models;

public sealed record RemoteReportInfo(
    string Id,
    string MachineId,
    string MachineName,
    string TestType,
    DateTime CreatedAt,
    string Duration,
    string Status,
    string Result,
    long PdfSizeBytes);
