using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Scheduling;

/// <summary>Updates an existing shift's rule values. Code is immutable. (FR-SFT-002.)</summary>
public sealed record UpdateShiftCommand(
    long Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int OvertimeThresholdMinutes) : IRequest<ShiftDto>;

public sealed class UpdateShiftValidator : AbstractValidator<UpdateShiftCommand>
{
    public UpdateShiftValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BreakMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceInMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GraceOutMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OvertimeThresholdMinutes).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateShiftHandler : IRequestHandler<UpdateShiftCommand, ShiftDto>
{
    private readonly ISchedulingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateShiftHandler(ISchedulingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftDto> Handle(UpdateShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await _repository.GetShiftByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Shift", request.Id);

        shift.UpdateDetails(
            request.Name, request.StartTime, request.EndTime,
            request.BreakMinutes, request.GraceInMinutes, request.GraceOutMinutes, request.OvertimeThresholdMinutes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ShiftDto.FromEntity(shift);
    }
}

/// <summary>Activates or deactivates a shift. (FR-SFT-002.)</summary>
public sealed record SetShiftActiveCommand(long Id, bool Active) : IRequest<ShiftDto>;

public sealed class SetShiftActiveHandler : IRequestHandler<SetShiftActiveCommand, ShiftDto>
{
    private readonly ISchedulingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetShiftActiveHandler(ISchedulingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftDto> Handle(SetShiftActiveCommand request, CancellationToken cancellationToken)
    {
        var shift = await _repository.GetShiftByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Shift", request.Id);

        if (request.Active) shift.Reactivate();
        else shift.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ShiftDto.FromEntity(shift);
    }
}
