using FluentValidation;
using MediatR;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Reporting;

/// <summary>Upper bound on any single export's date window (days), so a caller
/// cannot request the entire history in one unbounded query. (Perf hardening; NFR-02.)</summary>
internal static class ExportLimits
{
    public const int MaxWindowDays = 366;
}

// --- Export daily attendance (CSV) — FR-RPT-004, audited FR-RPT-007 ---
public sealed record ExportDailyAttendanceCommand(
    DateOnly? FromDate, DateOnly? ToDate, long? EmployeeId, long? DepartmentId, string? Status)
    : IRequest<ExportFile>;

public sealed class ExportDailyAttendanceValidator : AbstractValidator<ExportDailyAttendanceCommand>
{
    public ExportDailyAttendanceValidator()
    {
        // Exports MUST be bounded: require both ends of the window and cap its span,
        // so we never materialize the whole AttendanceRecord table into memory.
        RuleFor(x => x.FromDate).NotNull().WithMessage("An export requires a start date.");
        RuleFor(x => x.ToDate).NotNull().WithMessage("An export requires an end date.");
        RuleFor(x => x)
            .Must(x => x.FromDate is null || x.ToDate is null || x.ToDate >= x.FromDate)
            .WithMessage("The end date must not be before the start date.")
            .Must(x => x.FromDate is null || x.ToDate is null
                || x.ToDate.Value.DayNumber - x.FromDate.Value.DayNumber <= ExportLimits.MaxWindowDays)
            .WithMessage($"The export window must not exceed {ExportLimits.MaxWindowDays} days.");
    }
}

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
