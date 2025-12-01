using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record UserRightsMessage : IComposer
{
    public required ClubLevelType ClubLevel { get; init; }
    public required SecurityLevelType SecurityLevel { get; init; }
    public required bool IsAmbassador { get; init; }
}
