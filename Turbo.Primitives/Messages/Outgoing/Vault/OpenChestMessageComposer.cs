using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>Tells the client to open a wired chest's window.</summary>
[GenerateSerializer, Immutable]
public sealed record OpenChestMessageComposer : IComposer
{
    [Id(0)]
    public required int ChestId { get; init; }
}
