using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Scheduling;

namespace TAMS.Application.Scheduling;

/// <summary>Assigns a shift to an employee or department, effective-dated. (FR-SFT-003.)</summary>
public sealed record AssignShiftCommand(
    long ShiftId,
    long? EmployeeId,
    long? DepartmentId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo) : IRequest<ShiftAssignmentDto>;

public sealed class AssignShiftValidator : AbstractValidator<AssignShiftCommand>
{
    public AssignShiftValidator()
    {
        RuleFor(x => x.ShiftId).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => (x.EmployeeId is null) != (x.DepartmentId is null))
            .WithMessage("Assign to exactly one of employee or department.");
    }
}

public sealed class AssignShiftHandler : IRequestHandler<AssignShiftCommand, ShiftAssignmentDto>
{
    private readonly ISchedulingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignShiftHandler(ISchedulingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftAssignmentDto> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.GetShiftByIdAsync(request.ShiftId, cancellationToken) is null)
        {
            throw new BusinessRuleException($"Shift '{request.ShiftId}' does not exist.");
        }

        // Reject overlapping active assignments for the same target. (FR-SFT-003.)
        var existing = await _repository.GetAssignmentsForTargetAsync(
            request.EmployeeId, request.DepartmentId, cancellationToken);
        if (existing.Any(a => a.Overlaps(request.EffectiveFrom, request.EffectiveTo)))
        {
            throw new ConflictException("An overlapping shift assignment already exists for this target.");
        }

        var assignment = request.EmployeeId is not null
            ? ShiftAssignment.ForEmployee(request.ShiftId, request.EmployeeId.Value, request.EffectiveFrom, request.EffectiveTo)
            : ShiftAssignment.ForDepartment(request.ShiftId, request.DepartmentId!.Value, request.EffectiveFrom, request.EffectiveTo);

        await _repository.AddAssignmentAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ShiftAssignmentDto.FromEntity(assignment);
    }
}
