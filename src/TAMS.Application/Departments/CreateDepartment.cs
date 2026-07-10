using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Workforce;

namespace TAMS.Application.Departments;

/// <summary>Creates a department. (FR-DEP-001.)</summary>
public sealed record CreateDepartmentCommand(string Code, string Name, long? ParentDepartmentId)
    : IRequest<DepartmentDto>;

public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateDepartmentHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IDepartmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDepartmentHandler(IDepartmentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.CodeExistsAsync(request.Code, cancellationToken))
        {
            throw new ConflictException($"A department with code '{request.Code}' already exists.");
        }

        if (request.ParentDepartmentId is not null &&
            !await _repository.ExistsAsync(request.ParentDepartmentId.Value, cancellationToken))
        {
            throw new BusinessRuleException($"Parent department '{request.ParentDepartmentId}' does not exist.");
        }

        var department = new Department(request.Code, request.Name, request.ParentDepartmentId);
        await _repository.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DepartmentDto.FromEntity(department);
    }
}
