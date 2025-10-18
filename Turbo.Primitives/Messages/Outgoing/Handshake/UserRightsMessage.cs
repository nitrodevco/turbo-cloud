using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UserRightsMessage : IComposer
{
    public required ClubLevelEnum ClubLevel { get; init; }
    public required SecurityLevelEnum SecurityLevel { get; init; }
    public required bool IsAmbassador { get; init; }
}
