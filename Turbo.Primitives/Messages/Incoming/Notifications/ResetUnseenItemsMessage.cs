using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Notifications;

/// <summary>The client opened an inventory tab: nothing in it is new any more.</summary>
public record ResetUnseenItemsMessage : IMessageEvent
{
    /// <summary>The client's category number, unchecked; the handler maps it.</summary>
    public required int Category { get; init; }
}
