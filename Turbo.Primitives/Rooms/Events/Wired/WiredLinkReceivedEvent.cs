using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A message or dataframe arrived through this room's WIRED Links <see cref="LinkIds"/>; the
/// Receive Link Message or Receive Link Dataframe triggers listening through one of them fire.
/// </summary>
[GenerateSerializer]
public sealed record WiredLinkReceivedEvent : RoomEvent
{
    [Id(0)]
    public required ImmutableArray<int> LinkIds { get; init; }

    [Id(1)]
    public required WiredLinkPayload Payload { get; init; }
}
