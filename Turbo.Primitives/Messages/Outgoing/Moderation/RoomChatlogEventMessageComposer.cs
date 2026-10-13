using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A room's chatlog: one record.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomChatlogEventMessageComposer : IComposer
{
    [Id(0)]
    public required ModChatlogRecordSnapshot Record { get; init; }
}
