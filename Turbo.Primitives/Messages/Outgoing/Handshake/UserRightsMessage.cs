using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

[GenerateSerializer, Immutable]
public sealed record UserRightsMessage : IComposer
{
    [Id(0)]
    public required ClubLevelType ClubLevel { get; init; }

    [Id(1)]
    public required SecurityLevelType SecurityLevel { get; init; }

    [Id(2)]
    public required bool IsAmbassador { get; init; }
}
