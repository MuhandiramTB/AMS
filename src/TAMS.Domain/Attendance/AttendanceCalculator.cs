using TAMS.Domain.Scheduling;

namespace TAMS.Domain.Attendance;

/// <summary>
/// Pure domain service that computes a day's attendance from raw punches, the
/// effective shift, and whether approved leave covers the day. No I/O — given the
/// same inputs it always returns the same result, so it is exhaustively unit-
/// testable and safe to recompute. This is the accuracy-critical core (ADR-006,
/// G-01). Times are UTC (04 DP-07).
/// </summary>
public sealed class AttendanceCalculator
{
    /// <summary>
    /// Computes the attendance result for one employee-day.
    /// </summary>
    /// <param name="workDate">The attendance day.</param>
    /// <param name="shift">The shift in force on that day, or null if none assigned.</param>
    /// <param name="punches">All punches attributable to the day (any order).</param>
    /// <param name="isLeaveCovered">True if approved leave covers the day (BRULE-06).</param>
    public AttendanceResult Calculate(
        DateOnly workDate,
        Shift? shift,
        IReadOnlyList<PunchInput> punches,
        bool isLeaveCovered = false)
    {
        // Leave overrides absence: a covered day is not flagged, regardless of punches. (BRULE-06, FR-ATT-007.)
        if (isLeaveCovered && punches.Count == 0)
        {
            return new AttendanceResult { Status = AttendanceStatus.Processed };
        }

        // No punches and no leave → absent, surfaced as a missing-in exception.
        if (punches.Count == 0)
        {
            return new AttendanceResult
            {
                Status = AttendanceStatus.Exception,
                Exceptions = [AttendanceExceptionType.MissingIn]
            };
        }

        var ordered = punches.OrderBy(p => p.PunchedAtUtc).ToList();

        // Resolve first-in / last-out. Prefer explicit In/Out directions; fall back
        // to earliest/latest when directions are Unknown (device didn't tag them).
        var firstIn = ordered.FirstOrDefault(p => p.Direction == PunchDirection.In)?.PunchedAtUtc
            ?? ordered.First().PunchedAtUtc;

        // For the out: prefer an explicit Out punch; otherwise, if there is more
        // than one punch, treat the latest as the out (untagged in/out pair). A
        // single untagged punch has no out → missing-out (handled below).
        DateTime? lastOut = ordered.LastOrDefault(p => p.Direction == PunchDirection.Out)?.PunchedAtUtc;
        if (lastOut is null && ordered.Count > 1 && ordered.All(p => p.Direction == PunchDirection.Unknown))
        {
            lastOut = ordered.Last().PunchedAtUtc;
        }

        var exceptions = new List<AttendanceExceptionType>();

        // A lone punch (or no Out after In) → missing-out exception, no worked total.
        if (lastOut is null || lastOut <= firstIn)
        {
            exceptions.Add(AttendanceExceptionType.MissingOut);
            return new AttendanceResult
            {
                FirstInUtc = firstIn,
                LastOutUtc = null,
                WorkedMinutes = null,
                Status = AttendanceStatus.Exception,
                Exceptions = exceptions
            };
        }

        var grossMinutes = (int)Math.Round((lastOut.Value - firstIn).TotalMinutes);
        var breakMinutes = shift?.BreakMinutes ?? 0;
        var workedMinutes = Math.Max(0, grossMinutes - breakMinutes);

        int lateMinutes = 0;
        int earlyLeaveMinutes = 0;
        int overtimeMinutes = 0;

        if (shift is not null)
        {
            var (scheduledStart, scheduledEnd) = shift.ResolveWindow(workDate);

            // Late: arrival after start + grace.
            var lateBy = (int)Math.Round((firstIn - scheduledStart).TotalMinutes) - shift.GraceInMinutes;
            lateMinutes = Math.Max(0, lateBy);

            // Early leave: departure before end, beyond out-grace.
            var earlyBy = (int)Math.Round((scheduledEnd - lastOut.Value).TotalMinutes) - shift.GraceOutMinutes;
            earlyLeaveMinutes = Math.Max(0, earlyBy);

            // Overtime: worked beyond scheduled end. The threshold is a floor that
            // must be crossed before ANY overtime accrues, and only the minutes past
            // that threshold are credited (not the whole beyond-end span).
            var beyondEnd = (int)Math.Round((lastOut.Value - scheduledEnd).TotalMinutes);
            if (beyondEnd > shift.OvertimeThresholdMinutes)
            {
                overtimeMinutes = beyondEnd - shift.OvertimeThresholdMinutes;
            }

            // A punch-in wildly outside the shift window is flagged for review.
            if (firstIn < scheduledStart.AddHours(-12) || firstIn > scheduledEnd.AddHours(12))
            {
                exceptions.Add(AttendanceExceptionType.OutOfShift);
            }
        }

        return new AttendanceResult
        {
            FirstInUtc = firstIn,
            LastOutUtc = lastOut,
            WorkedMinutes = workedMinutes,
            LateMinutes = lateMinutes,
            EarlyLeaveMinutes = earlyLeaveMinutes,
            OvertimeMinutes = overtimeMinutes,
            Status = exceptions.Count > 0 ? AttendanceStatus.Exception : AttendanceStatus.Processed,
            Exceptions = exceptions
        };
    }
}
