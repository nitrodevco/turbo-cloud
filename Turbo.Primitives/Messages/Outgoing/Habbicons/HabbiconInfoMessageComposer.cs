using Orleans;
using Turbo.Primitives.Habbicons.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Habbicons;

/// <summary>One Habbicon with the player's state, the answer to <c>GetHabbiconInfo</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record HabbiconInfoMessageComposer : IComposer
{
    [Id(0)]
    public required HabbiconSnapshot Habbicon { get; init; }
}
