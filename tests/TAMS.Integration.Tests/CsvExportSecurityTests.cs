using System.Text;
using FluentAssertions;
using TAMS.Application.Common.Ports;
using TAMS.Infrastructure.Reporting;

namespace TAMS.Integration.Tests;

/// <summary>
/// CSV formula-injection defence for exports (OWASP CSV injection). A field whose
/// value starts with a formula trigger (=, +, -, @) must be neutralised so a
/// spreadsheet renders it as text instead of executing it. Pure unit test — no DB.
/// </summary>
public sealed class CsvExportSecurityTests
{
    private readonly CsvReportExporter _exporter = new();

    [Theory]
    [InlineData("=cmd|' /c calc'!A1")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1:A9)")]
    public void PayrollExport_NeutralisesFormulaTriggersInEmployeeName(string maliciousName)
    {
        var lines = new[]
        {
            new PayrollLine(1, "E001", maliciousName, TotalWorkedMinutes: 480, TotalOvertimeMinutes: 0, DaysPresent: 1),
        };

        var file = _exporter.ExportPayroll(lines, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var csv = Encoding.UTF8.GetString(file.Content);

        // The raw trigger must never appear as the first char of a field; our defence
        // prefixes a tab (and then quotes, since the value now contains a tab-led string).
        csv.Should().Contain("\t" + maliciousName.Substring(0, 1));
        // And the exact unescaped formula must not sit at a field boundary (",=" etc.).
        csv.Should().NotContain("," + maliciousName);
    }

    [Fact]
    public void PayrollExport_LeavesOrdinaryNamesUnchanged()
    {
        var lines = new[]
        {
            new PayrollLine(1, "E001", "Alice Smith", TotalWorkedMinutes: 480, TotalOvertimeMinutes: 60, DaysPresent: 1),
        };
        var csv = Encoding.UTF8.GetString(
            _exporter.ExportPayroll(lines, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)).Content);

        csv.Should().Contain("Alice Smith");
        csv.Should().NotContain("\tAlice");
    }
}
