using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Common;

namespace TAMS.Application.Attendance;

/// <summary>
/// Records a punch (manual entry in P2; device-fed in P3). Ingestion is
/// idempotent: a deterministic key over (device, deviceUserId, time, direction)
/// makes re-submitting the same punch a no-op. (FR-ATT-001/008, ADR-011.)
/// </summary>
public sealed record RecordPunchCommand(
    long DeviceId,
    string DeviceUserId,
    long? EmployeeId,
    DateTime PunchedAtUtc,
    PunchDirection Direction,
    PunchSource SourceType) : IRequest<bool>;

public sealed class RecordPunchValidator : AbstractValidator<RecordPunchCommand>
{
    public RecordPunchValidator()
    {
        RuleFor(x => x.DeviceUserId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.PunchedAtUtc).NotEmpty();
    }
}

public sealed class RecordPunchHandler : IRequestHandler<RecordPunchCommand, bool>
{
    private readonly IAttendanceRepository _attendance;
    private readonly IClock _clock;

    public RecordPunchHandler(IAttendanceRepository attendance, IClock clock)
    {
        _attendance = attendance;
        _clock = clock;
    }

    public async Task<bool> Handle(RecordPunchCommand request, CancellationToken cancellationToken)
    {
        var key = BuildIdempotencyKey(request);
        var punch = new PunchTransaction(
            request.DeviceId,
            request.DeviceUserId,
            request.EmployeeId,
            request.PunchedAtUtc,
            request.Direction,
            request.SourceType,
            key,
            _clock.UtcNow);

        // TryAddPunchAsync persists atomically and is race-safe (see the port).
        return await _attendance.TryAddPunchAsync(punch, cancellationToken);
        // false ⇒ duplicate ignored (idempotent)
    }

    private static string BuildIdempotencyKey(RecordPunchCommand r)
    {
        var raw = $"{r.DeviceId}|{r.DeviceUserId}|{r.PunchedAtUtc:O}|{(byte)r.Direction}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(hash);
    }
}
