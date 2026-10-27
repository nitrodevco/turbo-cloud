using Turbo.Primitives.Events;

namespace Turbo.Contracts.Approvals;

/// <summary>Something was asked for and waits for a person's decision.</summary>
public sealed record ApprovalRequestedEvent : DomainEvent
{
    public required Approval Approval { get; init; }
}

/// <summary>
/// A request was approved, denied or expired (<see cref="Approval"/>'s state: approved before it
/// is carried out, then <see cref="ApprovalExecutedEvent"/>). Raised once per request.
/// </summary>
public sealed record ApprovalDecidedEvent : DomainEvent
{
    public required Approval Approval { get; init; }
}

/// <summary>An approved request was carried out, or carrying it out failed.</summary>
public sealed record ApprovalExecutedEvent : DomainEvent
{
    public required Approval Approval { get; init; }

    public bool Succeeded => Approval.State == ApprovalState.Executed;
}
