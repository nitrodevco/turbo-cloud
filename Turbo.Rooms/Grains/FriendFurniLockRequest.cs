using Turbo.Primitives.Players;

namespace Turbo.Rooms.Grains;

/// <summary>A love lock waiting for both players to confirm.</summary>
public sealed class FriendFurniLockRequest
{
    public required PlayerId InitiatorId { get; init; }
    public required PlayerId PartnerId { get; init; }
    public bool InitiatorConfirmed { get; set; }
    public bool PartnerConfirmed { get; set; }

    public bool Involves(PlayerId playerId) => playerId == InitiatorId || playerId == PartnerId;

    public PlayerId OtherOf(PlayerId playerId) => playerId == InitiatorId ? PartnerId : InitiatorId;
}
