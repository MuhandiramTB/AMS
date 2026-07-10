namespace TAMS.Domain.Leave;

/// <summary>Leave request lifecycle. (02 §7.2 state machine, 04 §6.6.)</summary>
public enum LeaveStatus : byte
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Applied = 5
}
