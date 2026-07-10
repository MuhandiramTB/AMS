using MediatR;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Departments;

/// <summary>Lists departments, optionally filtered by parent. (FR-DEP query side.)</summary>
public sealed record GetDepartmentsQuery(long? ParentId) : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed class GetDepartmentsHandler
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    private readonly IDepartmentRepository _repository;

    public GetDepartmentsHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<DepartmentDto>> Handle(
        GetDepartmentsQuery request,
        CancellationToken cancellationToken)
    {
        var departments = await _repository.GetAllAsync(request.ParentId, cancellationToken);
        return departments.Select(DepartmentDto.FromEntity).ToList();
    }
}
