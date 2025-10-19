using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record CreateFlatMessage : IMessageEvent
{
    public string? FlatName { get; init; }
    public string? FlatDescription { get; init; }
    public string? FlatModelName { get; init; }
    public int CategoryID { get; init; }
    public int MaxPlayers { get; init; }
    public RoomTradeType TradeSetting { get; init; }
}
