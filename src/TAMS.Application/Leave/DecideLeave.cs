using MediatR;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Leave;

namespace TAMS.Application.Leave;

// --- Approve leave (FR-LV-002/004/005, BRULE-06/07) ---
public sealed record ApproveLeaveCommand(long RequestId, long ApproverUserId, bool AllowOverride = false)
    : IRequest<LeaveRequestDto>;

public sealed class ApproveLeaveHandler : IRequestHandler<ApproveLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ISender _mediator;

    public ApproveLeaveHandler(ILeaveRepository leave, IUnitOfWork unitOfWork, IClock clock, ISender mediator)
    {
        _leave = leave;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _mediator = mediator;
    }

    public async Task<LeaveRequestDto> Handle(ApproveLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leave.GetRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("LeaveRequest", request.RequestId);

        // Balance enforcement (BRULE-07): block approval beyond remaining unless overridden.
        var year = (short)leaveRequest.StartDate.Year;
        var balance = await _leave.GetBalanceAsync(
            leaveRequest.EmployeeId, leaveRequest.LeaveTypeId, year, cancellationToken);

        if (balance is null)
        {
            if (!request.AllowOverride)
            {
                throw new BusinessRuleException(
                    "No leave balance is set for this employee/type/year; approval requires an override.");
            }
        }
        else if (!request.AllowOverride && !balance.CanConsume(leaveRequest.DayCount))
        {
            throw new BusinessRuleException(
                $"Insufficient leave balance: requested {leaveRequest.DayCount}, remaining {balance.RemainingDays}.");
        }

        leaveRequest.Approve(request.ApproverUserId, _clock.UtcNow);
        balance?.Consume(leaveRequest.DayCount, request.AllowOverride);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reprocess attendance for the covered days so approved leave overrides
        // absence (BRULE-06, FR-ATT-007), then mark the request applied.
        for (var d = leaveRequest.StartDate; d <= leaveRequest.EndDate; d = d.AddDays(1))
        {
            await _mediator.Send(new ProcessAttendanceCommand(leaveRequest.EmployeeId, d), cancellationToken);
        }

        leaveRequest.MarkApplied();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return LeaveRequestDto.FromEntity(leaveRequest);
    }
}

// --- Reject leave ---
public sealed record RejectLeaveCommand(long RequestId, long ApproverUserId) : IRequest<LeaveRequestDto>;

public sealed class RejectLeaveHandler : IRequestHandler<RejectLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RejectLeaveHandler(ILeaveRepository leave, IUnitOfWork unitOfWork, IClock clock)
    {
        _leave = leave;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<LeaveRequestDto> Handle(RejectLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leave.GetRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("LeaveRequest", request.RequestId);

        leaveRequest.Reject(request.ApproverUserId, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return LeaveRequestDto.FromEntity(leaveRequest);
    }
}

// --- Cancel leave (releases balance if it had been consumed) ---
public sealed record CancelLeaveCommand(long RequestId) : IRequest<LeaveRequestDto>;

public sealed class CancelLeaveHandler : IRequestHandler<CancelLeaveCommand, LeaveRequestDto>
{
    private readonly ILeaveRepository _leave;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _mediator;

    public CancelLeaveHandler(ILeaveRepository leave, IUnitOfWork unitOfWork, ISender mediator)
    {
        _leave = leave;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task<LeaveRequestDto> Handle(CancelLeaveCommand request, CancellationToken cancellationToken)
    {
        var leaveRequest = await _leave.GetRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("LeaveRequest", request.RequestId);

        var wasApproved = leaveRequest.Status is LeaveStatus.Approved or LeaveStatus.Applied;
        var start = leaveRequest.StartDate;
        var end = leaveRequest.EndDate;
        var employeeId = leaveRequest.EmployeeId;

        leaveRequest.Cancel();

        // Return consumed days to the balance.
        if (wasApproved)
        {
            var year = (short)start.Year;
            var balance = await _leave.GetBalanceAsync(
                employeeId, leaveRequest.LeaveTypeId, year, cancellationToken);
            balance?.Release(leaveRequest.DayCount);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The days are no longer leave-covered → reprocess so attendance reflects
        // reality (e.g. an uncovered day with no punches becomes an exception again).
        if (wasApproved)
        {
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                await _mediator.Send(new Attendance.ProcessAttendanceCommand(employeeId, d), cancellationToken);
            }
        }

        return LeaveRequestDto.FromEntity(leaveRequest);
    }
}
