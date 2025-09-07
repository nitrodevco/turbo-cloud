using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Players;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public record UserRightsMessage : IComposer
{
    public ClubLevelEnum ClubLevel { get; init; }
    public SecurityLevelEnum SecurityLevel { get; init; }
    public bool IsAmbassador { get; init; }
}
