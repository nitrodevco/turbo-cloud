using Turbo.Primitives.Players;

namespace Turbo.Rooms.Grains.WiredTrading;

internal sealed class WiredTradeLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The trade going on now; null while there is none.</summary>
    public WiredTradeSession? Session { get; set; }
}
