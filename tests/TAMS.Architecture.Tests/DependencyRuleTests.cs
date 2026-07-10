using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace TAMS.Architecture.Tests;

/// <summary>
/// Mechanically enforces the Clean Architecture Dependency Rule (03 §7, ADR-004,
/// 07 §4). A violation fails the build, so boundaries can't erode silently.
/// </summary>
public sealed class DependencyRuleTests
{
    private const string Domain = "TAMS.Domain";
    private const string Application = "TAMS.Application";
    private const string Infrastructure = "TAMS.Infrastructure";
    private const string Api = "TAMS.Api";

    private static readonly Assembly DomainAssembly = typeof(TAMS.Domain.Common.Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(TAMS.Application.DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Domain must have no outward dependencies. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore_or_Aspnet()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Domain must be free of frameworks. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Application must depend only on the Domain. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Application must not reference EF Core directly (use ports). Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
