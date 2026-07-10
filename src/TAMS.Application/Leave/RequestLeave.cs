using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Application.Common.Security;
using TAMS.Domain.Identity;
using TAMS.Domain.Leave;

namespace TAMS.Application.Leave;

// --- Submit a leave request (FR-LV-001) ---
public sealed record RequestLeaveCommand(
    long EmployeeId, long LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string? Reason)
    : IRequest<LeaveRequestDto>;

public sealed class RequestLeaveValidator : AbstractValidator<RequestLeaveCommand>
{
    public RequestLeaveValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.LeaveTypeId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate)
            .WithMessage("End date cannot be before start date.");
    }
}

public sealed class RequestLeaveHandler : IRequestHandler<RequestLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public RequestLeaveHandler(
        ILeaveRepository leave, IEmployeeRepository employees, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _leave = leave;
        _employees = employees;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<LeaveRequestDto> Handle(RequestLeaveCommand request, CancellationToken cancellationToken)
    {
        // A caller without LeaveManage (i.e. not HR/Admin) may only file leave for
        // their OWN linked employee — the body EmployeeId cannot be used to submit
        // on someone else's behalf. (06 §5, OWASP A01 — broken access control.)
        if (!_currentUser.HasPermission(Permissions.LeaveManage))
        {
            if (_currentUser.EmployeeId is null)
            {
                throw new ForbiddenException("Your account is not linked to an employee record.");
            }
            if (request.EmployeeId != _currentUser.EmployeeId)
            {
                throw new ForbiddenException("You may only submit leave for yourself.");
            }
        }

        // Existence checks only — use AnyAsync so no tracked entity is materialized
        // on this hot write path. (Perf hardening.)
        if (!await _employees.ExistsAsync(request.EmployeeId, cancellationToken))
        {
            throw new BusinessRuleException($"Employee '{request.EmployeeId}' does not exist.");
        }

        if (!await _leave.TypeExistsAsync(request.LeaveTypeId, cancellationToken))
        {
            throw new BusinessRuleException($"Leave type '{request.LeaveTypeId}' does not exist.");
        }

        var leaveRequest = new LeaveRequest(
            request.EmployeeId, request.LeaveTypeId, request.StartDate, request.EndDate, request.Reason);
        await _leave.AddRequestAsync(leaveRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return LeaveRequestDto.FromEntity(leaveRequest);
    }
}

// --- List leave requests (FR-LV) ---
public sealed record GetLeaveRequestsQuery(int Page, int PageSize, long? EmployeeId, LeaveStatus? Status)
    : IRequest<PagedResult<LeaveRequestDto>>;

public sealed class GetLeaveRequestsValidator : AbstractValidator<GetLeaveRequestsQuery>
{
    public GetLeaveRequestsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetLeaveRequestsHandler : IRequestHandler<GetLeaveRequestsQuery, PagedResult<LeaveRequestDto>>
{
    private readonly ILeaveRepository _leave;
    private readonly ICurrentUser _currentUser;

    public GetLeaveRequestsHandler(ILeaveRepository leave, ICurrentUser currentUser)
    {
        _leave = leave;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        // Server-derived scope: without LeaveReadAll a caller only sees their own
        // requests, whatever employeeId they pass. (06 §5, OWASP A01 — IDOR.)
        var scope = DataScope.For(_currentUser, Permissions.LeaveReadAll);
        var employeeFilter = scope.ResolveEmployeeFilter(request.EmployeeId);

        var (items, total) = await _leave.GetRequestsPagedAsync(
            request.Page, request.PageSize, employeeFilter, request.Status, cancellationToken);
        return new PagedResult<LeaveRequestDto>(
            items.Select(LeaveRequestDto.FromEntity).ToList(), request.Page, request.PageSize, total);
    }
}
