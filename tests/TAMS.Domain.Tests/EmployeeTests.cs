using FluentAssertions;
using TAMS.Domain.Common;
using TAMS.Domain.Workforce;

namespace TAMS.Domain.Tests;

public sealed class EmployeeTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_SetsActiveAndSeedsStatusHistory()
    {
        var employee = new Employee("E1001", "Ann", "Silva", primaryDepartmentId: 3, Now, "ann@corp.com");

        employee.EmployeeNo.Should().Be("E1001");
        employee.FullName.Should().Be("Ann Silva");
        employee.Status.Should().Be(EmployeeStatus.Active);
        employee.IsActive.Should().BeTrue();
        employee.StatusHistory.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmployeeNo_Throws(string employeeNo)
    {
        var act = () => new Employee(employeeNo, "Ann", "Silva", 3, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithoutPrimaryDepartment_Throws()
    {
        // BRULE-01: exactly one primary department.
        var act = () => new Employee("E1001", "Ann", "Silva", primaryDepartmentId: 0, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deactivate_SetsInactiveAndRecordsHistory()
    {
        var employee = new Employee("E1001", "Ann", "Silva", 3, Now);

        employee.Deactivate(Now.AddDays(1), "Left the company");

        employee.IsActive.Should().BeFalse();
        employee.Status.Should().Be(EmployeeStatus.Inactive);
        employee.StatusHistory.Should().HaveCount(2);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_DoesNotAddHistory()
    {
        var employee = new Employee("E1001", "Ann", "Silva", 3, Now);

        employee.ChangeStatus(EmployeeStatus.Active, Now.AddHours(1), "no-op");

        employee.StatusHistory.Should().ContainSingle();
    }
}
