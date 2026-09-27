using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Notifications;

/// <summary>The client saw these items of one inventory tab.</summary>
public record ResetUnseenItemIdsMessage : IMessageEvent
{
    /// <summary>The client's category number, unchecked; the handler maps it.</summary>
    public required int Category { get; init; }

    public required List<int> ItemIds { get; init; }
}
