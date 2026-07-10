using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Workforce;

namespace TAMS.Application.Employees;

/// <summary>Creates an employee. (FR-EMP-001/002/003/006.)</summary>
public sealed record CreateEmployeeCommand(
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? Email,
    long PrimaryDepartmentId,
    DateOnly? HireDate) : IRequest<EmployeeDto>;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.EmployeeNo).NotEmpty().MaximumLength(32);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PrimaryDepartmentId).GreaterThan(0);
    }
}

public sealed class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employees;
    private readonly IDepartmentRepository _departments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateEmployeeHandler(
        IEmployeeRepository employees,
        IDepartmentRepository departments,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _employees = employees;
        _departments = departments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        if (await _employees.EmployeeNoExistsAsync(request.EmployeeNo, cancellationToken))
        {
            throw new ConflictException($"An employee with number '{request.EmployeeNo}' already exists.");
        }

        // Enforce BRULE-01: the primary department must exist.
        if (!await _departments.ExistsAsync(request.PrimaryDepartmentId, cancellationToken))
        {
            throw new BusinessRuleException($"Department '{request.PrimaryDepartmentId}' does not exist.");
        }

        var employee = new Employee(
            request.EmployeeNo,
            request.FirstName,
            request.LastName,
            request.PrimaryDepartmentId,
            _clock.UtcNow,
            request.Email,
            request.HireDate);

        await _employees.AddAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return EmployeeDto.FromEntity(employee);
    }
}
