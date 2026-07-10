using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Scheduling;

namespace TAMS.Application.Scheduling;

public sealed record CreateShiftCommand(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int OvertimeThresholdMinutes) : IRequest<ShiftDto>;

public sealed class CreateShiftValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BreakMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceInMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceOutMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OvertimeThresholdMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateShiftHandler : IRequestHandler<CreateShiftCommand, ShiftDto>
{
    private readonly ISchedulingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateShiftHandler(ISchedulingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftDto> Handle(CreateShiftCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.ShiftCodeExistsAsync(request.Code, cancellationToken))
        {
            throw new ConflictException($"A shift with code '{request.Code}' already exists.");
        }

        var shift = new Shift(
            request.Code, request.Name, request.StartTime, request.EndTime,
            request.BreakMinutes, request.GraceInMinutes, request.GraceOutMinutes,
            request.OvertimeThresholdMinutes);

        await _repository.AddShiftAsync(shift, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ShiftDto.FromEntity(shift);
    }
}

public sealed record GetShiftsQuery : IRequest<IReadOnlyList<ShiftDto>>;

public sealed class GetShiftsHandler : IRequestHandler<GetShiftsQuery, IReadOnlyList<ShiftDto>>
{
    private readonly ISchedulingRepository _repository;

    public GetShiftsHandler(ISchedulingRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<ShiftDto>> Handle(GetShiftsQuery request, CancellationToken cancellationToken)
    {
        var shifts = await _repository.GetShiftsAsync(cancellationToken);
        return shifts.Select(ShiftDto.FromEntity).ToList();
    }
}
