using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
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

    public RequestLeaveHandler(ILeaveRepository leave, IEmployeeRepository employees, IUnitOfWork unitOfWork)
    {
        _leave = leave;
        _employees = employees;
        _unitOfWork = unitOfWork;
    }

    public async Task<LeaveRequestDto> Handle(RequestLeaveCommand request, CancellationToken cancellationToken)
    {
        if (await _employees.GetByIdAsync(request.EmployeeId, cancellationToken) is null)
        {
            throw new BusinessRuleException($"Employee '{request.EmployeeId}' does not exist.");
        }

        if (await _leave.GetTypeByIdAsync(request.LeaveTypeId, cancellationToken) is null)
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
    public GetLeaveRequestsHandler(ILeaveRepository leave) => _leave = leave;

    public async Task<PagedResult<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _leave.GetRequestsPagedAsync(
            request.Page, request.PageSize, request.EmployeeId, request.Status, cancellationToken);
        return new PagedResult<LeaveRequestDto>(
            items.Select(LeaveRequestDto.FromEntity).ToList(), request.Page, request.PageSize, total);
    }
}
