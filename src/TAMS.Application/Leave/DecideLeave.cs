using MediatR;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Leave;

namespace TAMS.Application.Leave;

/// <summary>Splits a leave date range into (year, day-count) buckets, so a request
/// spanning a year boundary charges/releases each year's balance for its own days
/// rather than the whole request against the start year. (BRULE-07.)</summary>
internal static class LeaveYearSplit
{
    public static IReadOnlyList<(short Year, int Days)> DaysByYear(DateOnly start, DateOnly end)
    {
        var buckets = new Dictionary<short, int>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var y = (short)d.Year;
            buckets[y] = buckets.GetValueOrDefault(y) + 1;
        }
        return buckets.Select(kv => (kv.Key, kv.Value)).ToList();
    }
}

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

        // Balance enforcement (BRULE-07): charge each calendar year the days that fall
        // in it, so a request spanning a year boundary hits the right year's balance.
        var perYear = LeaveYearSplit.DaysByYear(leaveRequest.StartDate, leaveRequest.EndDate);
        var balances = new List<(Domain.Leave.LeaveBalance? Balance, int Days)>();
        foreach (var (year, days) in perYear)
        {
            var balance = await _leave.GetBalanceAsync(
                leaveRequest.EmployeeId, leaveRequest.LeaveTypeId, year, cancellationToken);

            if (balance is null)
            {
                if (!request.AllowOverride)
                {
                    throw new BusinessRuleException(
                        $"No {year} leave balance is set for this employee/type; approval requires an override.");
                }
            }
            else if (!request.AllowOverride && !balance.CanConsume(days))
            {
                throw new BusinessRuleException(
                    $"Insufficient {year} leave balance: requested {days}, remaining {balance.RemainingDays}.");
            }
            balances.Add((balance, days));
        }

        // Approve, consume balances, reprocess covered days, and mark applied all in ONE
        // transaction. Previously the balance consumption committed on its own SaveChanges;
        // if a later attendance reprocess threw, the balance stayed charged with the request
        // never applied. Wrapping keeps the whole decision atomic. (07 §4.2.)
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            leaveRequest.Approve(request.ApproverUserId, _clock.UtcNow);
            foreach (var (balance, days) in balances)
            {
                balance?.Consume(days, request.AllowOverride);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Reprocess attendance for the covered days so approved leave overrides
            // absence (BRULE-06, FR-ATT-007), then mark the request applied.
            for (var d = leaveRequest.StartDate; d <= leaveRequest.EndDate; d = d.AddDays(1))
            {
                await _mediator.Send(new ProcessAttendanceCommand(leaveRequest.EmployeeId, d), ct);
            }

            leaveRequest.MarkApplied();
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

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

        // Return consumed days to each year's balance (mirrors the per-year charge).
        if (wasApproved)
        {
            foreach (var (year, days) in LeaveYearSplit.DaysByYear(start, end))
            {
                var balance = await _leave.GetBalanceAsync(
                    employeeId, leaveRequest.LeaveTypeId, year, cancellationToken);
                balance?.Release(days);
            }
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
