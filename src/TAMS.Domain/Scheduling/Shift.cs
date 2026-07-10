using TAMS.Domain.Common;

namespace TAMS.Domain.Scheduling;

/// <summary>
/// A working window with tolerances and overtime policy. Supports overnight
/// shifts (end &lt; start ⇒ crosses midnight). Overtime policy is stored as data
/// (rules-as-data, FR-ADM-003); the calculator interprets it. (02 FR-SFT-*, 04 §6.3.)
/// </summary>
public sealed class Shift : AuditableEntity
{
    private Shift()
    {
    }

    public Shift(
        string code,
        string name,
        TimeOnly startTime,
        TimeOnly endTime,
        int breakMinutes = 0,
        int graceInMinutes = 0,
        int graceOutMinutes = 0,
        int overtimeThresholdMinutes = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Shift code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Shift name is required.");
        }

        if (breakMinutes < 0 || graceInMinutes < 0 || graceOutMinutes < 0 || overtimeThresholdMinutes < 0)
        {
            throw new DomainException("Shift minute values cannot be negative.");
        }

        Code = code.Trim();
        Name = name.Trim();
        StartTime = startTime;
        EndTime = endTime;
        BreakMinutes = breakMinutes;
        GraceInMinutes = graceInMinutes;
        GraceOutMinutes = graceOutMinutes;
        OvertimeThresholdMinutes = overtimeThresholdMinutes;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int BreakMinutes { get; private set; }
    public int GraceInMinutes { get; private set; }
    public int GraceOutMinutes { get; private set; }

    /// <summary>Minutes worked beyond scheduled end before overtime accrues.</summary>
    public int OvertimeThresholdMinutes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>True when the shift crosses midnight (e.g. 22:00–06:00). (FR-SFT-005.)</summary>
    public bool IsOvernight => EndTime <= StartTime;

    /// <summary>Scheduled duration in minutes, minus breaks, accounting for overnight.</summary>
    public int ScheduledMinutes
    {
        get
        {
            var minutes = IsOvernight
                ? (int)(TimeOnly.MaxValue.ToTimeSpan().TotalMinutes - StartTime.ToTimeSpan().TotalMinutes
                    + EndTime.ToTimeSpan().TotalMinutes + 1)
                : (int)(EndTime.ToTimeSpan().TotalMinutes - StartTime.ToTimeSpan().TotalMinutes);
            return Math.Max(0, minutes - BreakMinutes);
        }
    }

    /// <summary>
    /// Resolves the shift's scheduled start and end as absolute instants for a
    /// given work date. For overnight shifts the end falls on the next day.
    /// All times are treated as UTC (04 DP-07); local-zone handling is a
    /// presentation concern resolved before calling this.
    /// </summary>
    public (DateTime Start, DateTime End) ResolveWindow(DateOnly workDate)
    {
        var start = workDate.ToDateTime(StartTime, DateTimeKind.Utc);
        var endDate = IsOvernight ? workDate.AddDays(1) : workDate;
        var end = endDate.ToDateTime(EndTime, DateTimeKind.Utc);
        return (start, end);
    }

    public void Deactivate() => IsActive = false;
}
