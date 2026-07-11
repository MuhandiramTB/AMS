using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Departments;

/// <summary>Renames a department / re-parents it. (FR-ORG-002.)</summary>
public sealed record UpdateDepartmentCommand(long Id, string Name, long? ParentDepartmentId) : IRequest<DepartmentDto>;

public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x).Must(x => x.ParentDepartmentId != x.Id)
            .WithMessage("A department cannot be its own parent.");
    }
}

public sealed class UpdateDepartmentHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departments;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentHandler(IDepartmentRepository departments, IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _unitOfWork = unitOfWork;
    }

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _departments.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Department", request.Id);

        if (request.ParentDepartmentId is not null
            && !await _departments.ExistsAsync(request.ParentDepartmentId.Value, cancellationToken))
        {
            throw new BusinessRuleException($"Parent department '{request.ParentDepartmentId}' does not exist.");
        }

        department.Rename(request.Name);
        department.MoveUnder(request.ParentDepartmentId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return DepartmentDto.FromEntity(department);
    }
}

/// <summary>Activates or deactivates a department. A department with active
/// employees cannot be deactivated. (BRULE-02.)</summary>
public sealed record SetDepartmentActiveCommand(long Id, bool Active) : IRequest<DepartmentDto>;

public sealed class SetDepartmentActiveHandler : IRequestHandler<SetDepartmentActiveCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _departments;
    private readonly IUnitOfWork _unitOfWork;

    public SetDepartmentActiveHandler(IDepartmentRepository departments, IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _unitOfWork = unitOfWork;
    }

    public async Task<DepartmentDto> Handle(SetDepartmentActiveCommand request, CancellationToken cancellationToken)
    {
        var department = await _departments.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Department", request.Id);

        if (!request.Active && await _departments.HasActiveEmployeesAsync(request.Id, cancellationToken))
        {
            throw new BusinessRuleException("Cannot deactivate a department that still has active employees.");
        }

        if (request.Active) department.Reactivate();
        else department.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return DepartmentDto.FromEntity(department);
    }
}
