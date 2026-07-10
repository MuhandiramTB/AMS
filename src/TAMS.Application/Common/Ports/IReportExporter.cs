namespace TAMS.Application.Common.Ports;

/// <summary>A generated export file: bytes + content-type + filename.</summary>
public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Renders report data to a downloadable file. The default implementation emits
/// CSV; the concrete payroll-system format is pending OQ-04 and can be swapped
/// behind this port without touching the handlers. (FR-RPT-004/005, OQ-04.)
/// </summary>
public interface IReportExporter
{
    ExportFile ExportDailyAttendance(IReadOnlyList<DailyAttendanceRow> rows);

    ExportFile ExportPayroll(IReadOnlyList<PayrollLine> lines, DateOnly fromDate, DateOnly toDate);
}
