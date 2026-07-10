using System.Globalization;
using System.Text;
using TAMS.Application.Common.Ports;

namespace TAMS.Infrastructure.Reporting;

/// <summary>
/// Default CSV exporter. The payroll layout here is a reasonable placeholder; the
/// agreed payroll-system format (columns/order/encoding) is pending OQ-04 and can
/// replace this behind IReportExporter without touching the handlers. (FR-RPT-004/005.)
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public ExportFile ExportDailyAttendance(IReadOnlyList<DailyAttendanceRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("EmployeeNo,EmployeeName,DepartmentId,WorkDate,FirstInUtc,LastOutUtc,WorkedMinutes,LateMinutes,OvertimeMinutes,Status");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(r.EmployeeNo), Csv(r.EmployeeName), r.DepartmentId?.ToString() ?? "",
                r.WorkDate.ToString("yyyy-MM-dd"),
                r.FirstInUtc?.ToString("O") ?? "", r.LastOutUtc?.ToString("O") ?? "",
                r.WorkedMinutes?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.LateMinutes.ToString(CultureInfo.InvariantCulture),
                r.OvertimeMinutes.ToString(CultureInfo.InvariantCulture),
                Csv(r.Status)));
        }

        return new ExportFile(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "daily-attendance.csv");
    }

    public ExportFile ExportPayroll(IReadOnlyList<PayrollLine> lines, DateOnly fromDate, DateOnly toDate)
    {
        var sb = new StringBuilder();
        // Worked/OT reported in hours (2dp) — deterministic decimal, never float. (04 DP-11.)
        sb.AppendLine("EmployeeNo,EmployeeName,WorkedHours,OvertimeHours,DaysPresent,PeriodFrom,PeriodTo");
        foreach (var l in lines)
        {
            sb.AppendLine(string.Join(',',
                Csv(l.EmployeeNo), Csv(l.EmployeeName),
                Hours(l.TotalWorkedMinutes), Hours(l.TotalOvertimeMinutes),
                l.DaysPresent.ToString(CultureInfo.InvariantCulture),
                fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd")));
        }

        var name = $"payroll_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";
        return new ExportFile(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    private static string Hours(int minutes) =>
        (minutes / 60m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Minimal CSV field escaping (quote if it contains comma/quote/newline).</summary>
    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
