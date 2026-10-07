using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user adds inventory items to, or removes them from, their side of a wired trade.</summary>
public record WiredTradeAddDeleteItemsMessage : IMessageEvent
{
    public required bool IsDelete { get; init; }
    public required ImmutableArray<int> ItemIds { get; init; }
}
