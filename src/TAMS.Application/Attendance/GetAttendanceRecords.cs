using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Application.Common.Security;
using TAMS.Domain.Identity;

namespace TAMS.Application.Attendance;

public sealed record GetAttendanceRecordByIdQuery(long Id) : IRequest<AttendanceRecordDto>;

public sealed class GetAttendanceRecordByIdHandler
    : IRequestHandler<GetAttendanceRecordByIdQuery, AttendanceRecordDto>
{
    private readonly IAttendanceRepository _attendance;

    public GetAttendanceRecordByIdHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<AttendanceRecordDto> Handle(GetAttendanceRecordByIdQuery request, CancellationToken cancellationToken)
    {
        var record = await _attendance.GetRecordByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("AttendanceRecord", request.Id);
        return AttendanceRecordDto.FromEntity(record);
    }
}

public sealed record GetAttendanceRecordsQuery(
    int Page, int PageSize, long? EmployeeId, DateOnly? FromDate, DateOnly? ToDate)
    : IRequest<PagedResult<AttendanceRecordDto>>;

public sealed class GetAttendanceRecordsValidator : AbstractValidator<GetAttendanceRecordsQuery>
{
    public GetAttendanceRecordsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetAttendanceRecordsHandler
    : IRequestHandler<GetAttendanceRecordsQuery, PagedResult<AttendanceRecordDto>>
{
    private readonly IAttendanceRepository _attendance;
    private readonly ICurrentUser _currentUser;

    public GetAttendanceRecordsHandler(IAttendanceRepository attendance, ICurrentUser currentUser)
    {
        _attendance = attendance;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AttendanceRecordDto>> Handle(
        GetAttendanceRecordsQuery request, CancellationToken cancellationToken)
    {
        // Server-derived scope: restricted callers may only see their own records,
        // regardless of any employeeId they pass. (06 §5, OWASP A01.)
        var scope = DataScope.For(_currentUser, Permissions.AttendanceReadAll);
        var employeeFilter = scope.ResolveEmployeeFilter(request.EmployeeId);

        var (items, total) = await _attendance.GetRecordsPagedAsync(
            request.Page, request.PageSize, employeeFilter, request.FromDate, request.ToDate, cancellationToken);

        return new PagedResult<AttendanceRecordDto>(
            items.Select(AttendanceRecordDto.FromEntity).ToList(),
            request.Page, request.PageSize, total);
    }
}
