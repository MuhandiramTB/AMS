using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;

namespace TAMS.Application.Employees;

/// <summary>Updates an employee's editable details + primary department. (FR-EMP-004.)</summary>
public sealed record UpdateEmployeeCommand(
    long Id,
    string FirstName,
    string LastName,
    string? Email,
    long PrimaryDepartmentId) : IRequest<EmployeeDto>;

public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PrimaryDepartmentId).GreaterThan(0);
    }
}

public sealed class UpdateEmployeeHandler : IRequestHandler<UpdateEmployeeCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employees;
    private readonly IDepartmentRepository _departments;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeHandler(IEmployeeRepository employees, IDepartmentRepository departments, IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _departments = departments;
        _unitOfWork = unitOfWork;
    }

    public async Task<EmployeeDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Employee", request.Id);

        if (!await _departments.ExistsAsync(request.PrimaryDepartmentId, cancellationToken))
        {
            throw new BusinessRuleException($"Department '{request.PrimaryDepartmentId}' does not exist.");
        }

        employee.UpdateDetails(request.FirstName, request.LastName, request.Email);
        employee.ChangePrimaryDepartment(request.PrimaryDepartmentId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return EmployeeDto.FromEntity(employee);
    }
}

/// <summary>Activates or deactivates an employee. (FR-EMP-005.)</summary>
public sealed record SetEmployeeActiveCommand(long Id, bool Active, string? Reason) : IRequest<EmployeeDto>;

public sealed class SetEmployeeActiveHandler : IRequestHandler<SetEmployeeActiveCommand, EmployeeDto>
{
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SetEmployeeActiveHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork, IClock clock)
    {
        _employees = employees;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<EmployeeDto> Handle(SetEmployeeActiveCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Employee", request.Id);

        if (request.Active)
        {
            employee.ChangeStatus(Domain.Workforce.EmployeeStatus.Active, _clock.UtcNow, request.Reason ?? "Reactivated");
        }
        else
        {
            employee.Deactivate(_clock.UtcNow, request.Reason);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return EmployeeDto.FromEntity(employee);
    }
}
