using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Employees;

/// <summary>Fetches a single employee by id. (FR-EMP-001.)</summary>
public sealed record GetEmployeeByIdQuery(long Id) : IRequest<EmployeeDto>;

public sealed class GetEmployeeByIdHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDto>
{
    private readonly IEmployeeRepository _employees;

    public GetEmployeeByIdHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<EmployeeDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Employee", request.Id);

        return EmployeeDto.FromEntity(employee);
    }
}
