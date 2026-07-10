namespace TAMS.Domain.Attendance;

/// <summary>Direction of a punch. (04 §6.5.)</summary>
public enum PunchDirection : byte
{
    Unknown = 0,
    In = 1,
    Out = 2
}

/// <summary>Origin of a punch. (04 §6.5.)</summary>
public enum PunchSource : byte
{
    Device = 1,
    Realtime = 2,
    ManualEntry = 3
}

/// <summary>Attendance record lifecycle. (02 §7.1 state machine, 04 §6.5.)</summary>
public enum AttendanceStatus : byte
{
    Pending = 1,
    Processed = 2,
    Exception = 3,
    UnderReview = 4,
    Corrected = 5,
    Finalized = 6
}

/// <summary>Types of attendance anomaly requiring review. (FR-ATT-005.)</summary>
public enum AttendanceExceptionType : byte
{
    MissingIn = 1,
    MissingOut = 2,
    OutOfShift = 3,
    Duplicate = 4,
    Anomaly = 5
}
