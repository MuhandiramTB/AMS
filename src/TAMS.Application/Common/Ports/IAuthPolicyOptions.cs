namespace TAMS.Application.Common.Ports;

/// <summary>
/// Auth policy values sourced from configuration (12-Factor). Kept as a port so
/// the Application layer stays free of config-framework types. (06 §6, 11 §5.)
/// </summary>
public interface IAuthPolicyOptions
{
    TimeSpan LockoutDuration { get; }
}
