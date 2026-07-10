using MediatR;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Reporting;

// --- Export daily attendance (CSV) — FR-RPT-004, audited FR-RPT-007 ---
public sealed record ExportDailyAttendanceCommand(
    DateOnly? FromDate, DateOnly? ToDate, long? EmployeeId, long? DepartmentId, string? Status)
    : IRequest<ExportFile>;

public sealed class ExportDailyAttendanceHandler : IRequestHandler<ExportDailyAttendanceCommand, ExportFile>
{
    private readonly IReportingRepository _reporting;
    private readonly IReportExporter _exporter;
    private readonly IAuditWriter _audit;

    public ExportDailyAttendanceHandler(IReportingRepository reporting, IReportExporter exporter, IAuditWriter audit)
    {
        _reporting = reporting;
        _exporter = exporter;
        _audit = audit;
    }

    public async Task<ExportFile> Handle(ExportDailyAttendanceCommand request, CancellationToken cancellationToken)
    {
        // Pull all matching rows (paged large) for the export.
        var (rows, _) = await _reporting.GetDailyAttendanceAsync(
            page: 1, pageSize: int.MaxValue, request.FromDate, request.ToDate,
            request.EmployeeId, request.DepartmentId, request.Status, cancellationToken);

        var file = _exporter.ExportDailyAttendance(rows);
        await _audit.RecordAsync("Report.Export.DailyAttendance", "Report", file.FileName, cancellationToken);
        return file;
    }
}

// --- Payroll export (worked hours + OT per employee) — FR-RPT-005, audited ---
public sealed record ExportPayrollCommand(DateOnly FromDate, DateOnly ToDate, long? DepartmentId)
    : IRequest<ExportFile>;

public sealed class ExportPayrollHandler : IRequestHandler<ExportPayrollCommand, ExportFile>
{
    private readonly IReportingRepository _reporting;
    private readonly IReportExporter _exporter;
    private readonly IAuditWriter _audit;

    public ExportPayrollHandler(IReportingRepository reporting, IReportExporter exporter, IAuditWriter audit)
    {
        _reporting = reporting;
        _exporter = exporter;
        _audit = audit;
    }

    public async Task<ExportFile> Handle(ExportPayrollCommand request, CancellationToken cancellationToken)
    {
        var lines = await _reporting.GetPayrollLinesAsync(
            request.FromDate, request.ToDate, request.DepartmentId, cancellationToken);

        var file = _exporter.ExportPayroll(lines, request.FromDate, request.ToDate);
        await _audit.RecordAsync(
            "Report.Export.Payroll", "Report", $"{request.FromDate:yyyy-MM-dd}_{request.ToDate:yyyy-MM-dd}", cancellationToken);
        return file;
    }
}
