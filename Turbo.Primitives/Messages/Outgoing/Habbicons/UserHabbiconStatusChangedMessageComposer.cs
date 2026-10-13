using Orleans;
using Turbo.Primitives.Habbicons.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Habbicons;

/// <summary>One of the player's Habbicons changed state (bought, earned, claimed, (un)favourited).</summary>
[GenerateSerializer, Immutable]
public sealed record UserHabbiconStatusChangedMessageComposer : IComposer
{
    [Id(0)]
    public required int HabbiconId { get; init; }

    [Id(1)]
    public required HabbiconState State { get; init; }
}
