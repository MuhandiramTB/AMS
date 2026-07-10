using System.Security.Cryptography;
using System.Text;

namespace TAMS.Domain.Attendance;

/// <summary>
/// Builds the deterministic idempotency key for a punch. Both the manual-entry
/// and device-sync ingestion paths use this so the same physical punch always
/// yields the same key, making storage exactly-once via the unique index.
/// (FR-ATT-008, FR-ZK-008, ADR-011.)
/// </summary>
public static class PunchIdempotency
{
    public static string BuildKey(long deviceId, string deviceUserId, DateTime punchedAtUtc, PunchDirection direction)
    {
        var raw = $"{deviceId}|{deviceUserId}|{punchedAtUtc:O}|{(byte)direction}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(hash);
    }
}
