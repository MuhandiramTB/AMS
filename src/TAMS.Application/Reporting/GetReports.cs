using FluentValidation;
using MediatR;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Application.Common.Security;
using TAMS.Domain.Identity;

namespace TAMS.Application.Reporting;

// --- Dashboard attendance summary (FR-RPT-001) ---
public sealed record GetAttendanceSummaryQuery(DateOnly WorkDate, long? DepartmentId)
    : IRequest<AttendanceSummaryDto>;

public sealed class GetAttendanceSummaryHandler : IRequestHandler<GetAttendanceSummaryQuery, AttendanceSummaryDto>
{
    private readonly IReportingRepository _reporting;
    private readonly ICurrentUser _currentUser;

    public GetAttendanceSummaryHandler(IReportingRepository reporting, ICurrentUser currentUser)
    {
        _reporting = reporting;
        _currentUser = currentUser;
    }

    public async Task<AttendanceSummaryDto> Handle(GetAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        // Server-derived scope: a caller without AttendanceReadAll only sees their own
        // day (or nothing if unlinked), never org/department-wide counts. (06 §5.)
        var scope = DataScope.For(_currentUser, Permissions.AttendanceReadAll);
        var employeeFilter = scope.IsUnrestricted ? null : scope.ResolveEmployeeFilter(null);

        var summary = await _reporting.GetAttendanceSummaryAsync(
            request.WorkDate, scope.IsUnrestricted ? request.DepartmentId : null, employeeFilter, cancellationToken);
        return AttendanceSummaryDto.From(summary);
    }
}

// --- Daily attendance report (FR-RPT-002/003), role-scoped ---
public sealed record GetDailyAttendanceQuery(
    int Page, int PageSize, DateOnly? FromDate, DateOnly? ToDate,
    long? EmployeeId, long? DepartmentId, string? Status) : IRequest<PagedResult<DailyAttendanceRowDto>>;

public sealed class GetDailyAttendanceValidator : AbstractValidator<GetDailyAttendanceQuery>
{
    public GetDailyAttendanceValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetDailyAttendanceHandler
    : IRequestHandler<GetDailyAttendanceQuery, PagedResult<DailyAttendanceRowDto>>
{
    private readonly IReportingRepository _reporting;
    private readonly ICurrentUser _currentUser;

    public GetDailyAttendanceHandler(IReportingRepository reporting, ICurrentUser currentUser)
    {
        _reporting = reporting;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<DailyAttendanceRowDto>> Handle(
        GetDailyAttendanceQuery request, CancellationToken cancellationToken)
    {
        // Server-derived scope: restricted callers only see their own rows. (06 §5.)
        var scope = DataScope.For(_currentUser, Permissions.AttendanceReadAll);
        var employeeFilter = scope.ResolveEmployeeFilter(request.EmployeeId);

        var (items, total) = await _reporting.GetDailyAttendanceAsync(
            request.Page, request.PageSize, request.FromDate, request.ToDate,
            employeeFilter, request.DepartmentId, request.Status, cancellationToken);

        return new PagedResult<DailyAttendanceRowDto>(
            items.Select(DailyAttendanceRowDto.From).ToList(), request.Page, request.PageSize, total);
    }
}

// --- Open exceptions report (FR-RPT-002) ---
public sealed record GetExceptionsReportQuery(DateOnly? FromDate, DateOnly? ToDate, long? DepartmentId)
    : IRequest<IReadOnlyList<ExceptionRowDto>>;

public sealed class GetExceptionsReportHandler
    : IRequestHandler<GetExceptionsReportQuery, IReadOnlyList<ExceptionRowDto>>
{
    private readonly IReportingRepository _reporting;
    private readonly ICurrentUser _currentUser;

    public GetExceptionsReportHandler(IReportingRepository reporting, ICurrentUser currentUser)
    {
        _reporting = reporting;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ExceptionRowDto>> Handle(GetExceptionsReportQuery request, CancellationToken cancellationToken)
    {
        // Server-derived scope (was an IDOR): a restricted caller only sees their own
        // exceptions and cannot widen via a client departmentId. (06 §5, OWASP A01.)
        var scope = DataScope.For(_currentUser, Permissions.AttendanceReadAll);
        var employeeFilter = scope.IsUnrestricted ? null : scope.ResolveEmployeeFilter(null);

        var rows = await _reporting.GetOpenExceptionsAsync(
            request.FromDate, request.ToDate, scope.IsUnrestricted ? request.DepartmentId : null,
            employeeFilter, cancellationToken);
        return rows.Select(ExceptionRowDto.From).ToList();
    }
}
