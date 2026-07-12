using FluentValidation;
using MediatR;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;

namespace TAMS.Application.Attendance;

public sealed record UnresolvedPunchDto(
    long Id,
    long DeviceId,
    string DeviceUserId,
    DateTime PunchedAtUtc,
    string Direction,
    string Source)
{
    public static UnresolvedPunchDto FromEntity(PunchTransaction p) =>
        new(p.Id, p.DeviceId, p.DeviceUserId, p.PunchedAtUtc, p.Direction.ToString(), p.SourceType.ToString());
}

public sealed record GetUnresolvedPunchesQuery(int Page, int PageSize, long? DeviceId)
    : IRequest<PagedResult<UnresolvedPunchDto>>;

public sealed class GetUnresolvedPunchesValidator : AbstractValidator<GetUnresolvedPunchesQuery>
{
    public GetUnresolvedPunchesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetUnresolvedPunchesHandler
    : IRequestHandler<GetUnresolvedPunchesQuery, PagedResult<UnresolvedPunchDto>>
{
    private readonly IAttendanceRepository _attendance;

    public GetUnresolvedPunchesHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<PagedResult<UnresolvedPunchDto>> Handle(
        GetUnresolvedPunchesQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _attendance.GetUnresolvedPunchesPagedAsync(
            request.Page, request.PageSize, request.DeviceId, cancellationToken);

        return new PagedResult<UnresolvedPunchDto>(
            items.Select(UnresolvedPunchDto.FromEntity).ToList(),
            request.Page, request.PageSize, total);
    }
}
