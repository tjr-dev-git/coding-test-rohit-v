namespace Dispatch.Api.Domain;

public enum MovementPriority
{
    Normal,
    Urgent
}

public enum MovementStatus
{
    Requested,
    Assigned,
    InProgress,
    Completed,
    Cancelled
}

public enum ShiftKind
{
    Day,
    Night
}

