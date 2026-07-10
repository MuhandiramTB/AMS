using FluentValidation;
using MediatR;
using TAMS.Application.Common.Models;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Employees;

/// <summary>Paged, filterable employee list. (FR-EMP query side, 05 §7.)</summary>
public sealed record GetEmployeesQuery(int Page, int PageSize, long? DepartmentId, string? Search)
    : IRequest<PagedResult<EmployeeDto>>;

public sealed class GetEmployeesValidator : AbstractValidator<GetEmployeesQuery>
{
    public GetEmployeesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100); // hard cap (05 §7)
    }
}

public sealed class GetEmployeesHandler : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeDto>>
{
    private readonly IEmployeeRepository _employees;

    public GetEmployeesHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<PagedResult<EmployeeDto>> Handle(
        GetEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _employees.GetPagedAsync(
            request.Page, request.PageSize, request.DepartmentId, request.Search, cancellationToken);

        return new PagedResult<EmployeeDto>(
            items.Select(EmployeeDto.FromEntity).ToList(),
            request.Page,
            request.PageSize,
            total);
    }
}
