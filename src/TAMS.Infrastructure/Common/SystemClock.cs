using TAMS.Domain.Common;

namespace TAMS.Infrastructure.Common;

/// <summary>Real system clock. Always UTC. (04 DP-07, 07 §10.)</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
