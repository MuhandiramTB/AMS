using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;

namespace TAMS.Application.Attendance;

/// <summary>
/// Applies an authorised manual correction to an attendance record's in/out time,
/// with a mandatory reason, preserving the original as an audited correction, then
/// re-derives the worked total. The raw punch is never touched. (FR-ATT-006, BRULE-05.)
/// </summary>
public sealed record CorrectAttendanceCommand(
    long RecordId,
    long ActorUserId,
    DateTime? FirstInUtc,
    DateTime? LastOutUtc,
    string Reason,
    // Base64 RowVersion from the client's If-Match header. Required for optimistic
    // concurrency: a stale token yields a 409. (05 §8.2, FR-ATT-006.)
    string? ExpectedConcurrencyToken) : IRequest<AttendanceRecordDto>;

public sealed class CorrectAttendanceValidator : AbstractValidator<CorrectAttendanceCommand>
{
    public CorrectAttendanceValidator()
    {
        RuleFor(x => x.RecordId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x)
            .Must(x => x.FirstInUtc is not null || x.LastOutUtc is not null)
            .WithMessage("At least one of FirstIn or LastOut must be provided.");
    }
}

public sealed class CorrectAttendanceHandler : IRequestHandler<CorrectAttendanceCommand, AttendanceRecordDto>
{
    private readonly IAttendanceRepository _attendance;
    private readonly ISchedulingRepository _scheduling;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CorrectAttendanceHandler(
        IAttendanceRepository attendance,
        ISchedulingRepository scheduling,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _attendance = attendance;
        _scheduling = scheduling;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AttendanceRecordDto> Handle(CorrectAttendanceCommand request, CancellationToken cancellationToken)
    {
        var record = await _attendance.GetRecordByIdAsync(request.RecordId, cancellationToken)
            ?? throw new NotFoundException("AttendanceRecord", request.RecordId);

        // Optimistic concurrency: enforce the client's If-Match token so a stale
        // edit fails with 409 rather than silently overwriting. (05 §8.2, FR-ATT-006.)
        if (!string.IsNullOrEmpty(request.ExpectedConcurrencyToken))
        {
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(request.ExpectedConcurrencyToken);
            }
            catch (FormatException)
            {
                throw new BusinessRuleException("Malformed concurrency token.");
            }

            _attendance.SetOriginalConcurrencyToken(record, expected);
        }

        var now = _clock.UtcNow;

        if (request.FirstInUtc is not null && request.FirstInUtc != record.FirstInUtc)
        {
            record.ApplyCorrection(
                request.ActorUserId, nameof(record.FirstInUtc),
                record.FirstInUtc?.ToString("O"), request.FirstInUtc.Value.ToString("O"),
                request.Reason, now);
            record.SetFirstIn(request.FirstInUtc.Value);
        }

        if (request.LastOutUtc is not null && request.LastOutUtc != record.LastOutUtc)
        {
            record.ApplyCorrection(
                request.ActorUserId, nameof(record.LastOutUtc),
                record.LastOutUtc?.ToString("O"), request.LastOutUtc.Value.ToString("O"),
                request.Reason, now);
            record.SetLastOut(request.LastOutUtc.Value);
        }

        // Re-derive worked minutes honouring the resolved shift's break. (FR-ATT-009.)
        var breakMinutes = 0;
        if (record.ResolvedShiftId is { } shiftId)
        {
            var shift = await _scheduling.GetShiftByIdAsync(shiftId, cancellationToken);
            breakMinutes = shift?.BreakMinutes ?? 0;
        }

        record.RecomputeWorkedFromInOut(breakMinutes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AttendanceRecordDto.FromEntity(record);
    }
}
