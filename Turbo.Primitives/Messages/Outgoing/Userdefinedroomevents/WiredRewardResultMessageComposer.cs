using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Tells a player why a "give reward" box gave them nothing.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredRewardResultMessageComposer : IComposer
{
    [Id(0)]
    public required WiredRewardResultType Reason { get; init; }
}
