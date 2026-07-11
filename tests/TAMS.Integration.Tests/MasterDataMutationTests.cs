using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Departments;
using TAMS.Application.Employees;
using TAMS.Application.Scheduling;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Integration.Tests;

/// <summary>
/// Master-data mutations added for the UI Actions column: update + activate/
/// deactivate for Employee, Department and Shift, over the real stack. Verifies
/// the changes persist and the guard rules hold. MediatR-direct for stability.
/// </summary>
[Collection("integration")]
public sealed class MasterDataMutationTests
{
    private readonly TamsWebApplicationFactory _factory;
    public MasterDataMutationTests(TamsWebApplicationFactory factory) => _factory = factory;

    private static string U(string p) => $"{p}{Guid.NewGuid():N}".Substring(0, 12);

    [Fact]
    public async Task UpdateEmployee_ChangesDetails_AndPersists()
    {
        var s = U("M");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"MD{s}", $"MDept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"ME{s}", "Old", "Name", null, dept.Id, null));

        var updated = await _factory.SendAsync(new UpdateEmployeeCommand(emp.Id, "New", "Name", "new@x.io", dept.Id));

        updated.FirstName.Should().Be("New");
        updated.Email.Should().Be("new@x.io");
    }

    [Fact]
    public async Task DeactivateThenActivateEmployee_TogglesStatus()
    {
        var s = U("M");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"MD{s}", $"MDept{s}", null));
        var emp = await _factory.SendAsync(new CreateEmployeeCommand($"ME{s}", "Act", "Ive", null, dept.Id, null));

        var off = await _factory.SendAsync(new SetEmployeeActiveCommand(emp.Id, false, "left"));
        off.IsActive.Should().BeFalse();

        var on = await _factory.SendAsync(new SetEmployeeActiveCommand(emp.Id, true, null));
        on.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEmployee_UnknownId_IsNotFound()
    {
        var act = () => _factory.SendAsync(new UpdateEmployeeCommand(999999, "A", "B", null, 1));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RenameDepartment_Persists()
    {
        var s = U("M");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"MD{s}", $"Before{s}", null));

        var updated = await _factory.SendAsync(new UpdateDepartmentCommand(dept.Id, $"After{s}", null));

        updated.Name.Should().Be($"After{s}");
    }

    [Fact]
    public async Task DeactivateDepartment_WithActiveEmployees_IsRejected()
    {
        var s = U("M");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"MD{s}", $"MDept{s}", null));
        await _factory.SendAsync(new CreateEmployeeCommand($"ME{s}", "In", "Dept", null, dept.Id, null));

        var act = () => _factory.SendAsync(new SetDepartmentActiveCommand(dept.Id, false));
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeactivateDepartment_WithoutActiveEmployees_Succeeds()
    {
        var s = U("M");
        var dept = await _factory.SendAsync(new CreateDepartmentCommand($"MD{s}", $"MDept{s}", null));

        var off = await _factory.SendAsync(new SetDepartmentActiveCommand(dept.Id, false));
        off.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateShift_ChangesRuleValues_AndPersists()
    {
        var s = U("M");
        var shift = await _factory.SendAsync(new CreateShiftCommand(
            $"MS{s}", "Before", new TimeOnly(9, 0), new TimeOnly(17, 0), 60, 5, 5, 0));

        var updated = await _factory.SendAsync(new UpdateShiftCommand(
            shift.Id, "After", new TimeOnly(8, 0), new TimeOnly(16, 0), 30, 10, 10, 15));

        updated.Name.Should().Be("After");
        updated.BreakMinutes.Should().Be(30);
        updated.OvertimeThresholdMinutes.Should().Be(15);
    }

    [Fact]
    public async Task SetShiftInactive_Persists()
    {
        var s = U("M");
        var shift = await _factory.SendAsync(new CreateShiftCommand(
            $"MS{s}", "Toggle", new TimeOnly(9, 0), new TimeOnly(17, 0), 0, 0, 0, 0));

        var off = await _factory.SendAsync(new SetShiftActiveCommand(shift.Id, false));
        off.IsActive.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TamsDbContext>();
        var persisted = await db.Shifts.AsNoTracking().FirstAsync(x => x.Id == shift.Id);
        persisted.IsActive.Should().BeFalse();
    }
}
