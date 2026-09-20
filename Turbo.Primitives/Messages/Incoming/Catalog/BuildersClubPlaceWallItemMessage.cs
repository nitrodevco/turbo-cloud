using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record BuildersClubPlaceWallItemMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public string? ExtraParam { get; init; }
    public string? Location { get; init; }

    /// <summary>See <see cref="BuildersClubPlaceRoomItemMessage.ConfirmedHideRoom"/>.</summary>
    public bool ConfirmedHideRoom { get; init; }
}
