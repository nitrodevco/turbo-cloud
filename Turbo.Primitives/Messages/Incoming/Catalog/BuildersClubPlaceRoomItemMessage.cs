using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record BuildersClubPlaceRoomItemMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public string? ExtraParam { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Direction { get; init; }

    /// <summary>
    /// The client re-sends the request with this set after the player agreed to
    /// <c>room.confirm.hide_room</c>, which is what a trial member is asked before borrowing
    /// anything. It is only ever true on a second attempt the server itself asked for.
    /// </summary>
    public bool ConfirmedHideRoom { get; init; }
}
