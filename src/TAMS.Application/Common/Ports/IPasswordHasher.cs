namespace TAMS.Application.Common.Ports;

/// <summary>
/// One-way password hashing/verification. Implementation uses a strong adaptive
/// algorithm; the domain/application never handle plaintext beyond this port.
/// (06 §4, FR-AUTH-004.)
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Performs a hash computation against a fixed dummy hash and discards the
    /// result. Used on the unknown-user login path so its timing matches a real
    /// verification, mitigating username-enumeration via timing. (06 §4.2.)
    /// </summary>
    void VerifyDummy(string password);
}
