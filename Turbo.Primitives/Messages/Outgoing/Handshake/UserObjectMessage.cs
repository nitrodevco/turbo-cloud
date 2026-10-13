using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record UserObjectMessage : IComposer
{
    [Id(0)]
    public required PlayerSummarySnapshot Player { get; init; }

    [Id(1)]
    public required int MaxRespectPerDay { get; init; }

    /// <summary>
    /// <c>nameChangeAllowed</c>: the player may still choose their name - the own avatar menu's
    /// "change name". Habbo sends it true to a new user whose name is still the generated one.
    /// </summary>
    [Id(2)]
    public bool NameChangeAllowed { get; init; }
}
