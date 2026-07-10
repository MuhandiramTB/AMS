using TAMS.Domain.Common;

namespace TAMS.Domain.Tests;

/// <summary>Deterministic clock for tests. (07 §10 — never read the real clock.)</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}
