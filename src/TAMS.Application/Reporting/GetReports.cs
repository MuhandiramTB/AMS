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
    public GetAttendanceSummaryHandler(IReportingRepository reporting) => _reporting = reporting;

    public async Task<AttendanceSummaryDto> Handle(GetAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _reporting.GetAttendanceSummaryAsync(request.WorkDate, request.DepartmentId, cancellationToken);
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
    public GetExceptionsReportHandler(IReportingRepository reporting) => _reporting = reporting;

    public async Task<IReadOnlyList<ExceptionRowDto>> Handle(GetExceptionsReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await _reporting.GetOpenExceptionsAsync(request.FromDate, request.ToDate, request.DepartmentId, cancellationToken);
        return rows.Select(ExceptionRowDto.From).ToList();
    }
}
