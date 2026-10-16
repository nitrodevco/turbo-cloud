using Orleans;

namespace Turbo.Primitives.Players;

/// <summary>
/// A player's direct composers held back until a room publish carrying this hold reaches their
/// presence. A grain that tells a room to send something, without awaiting it, and then sends the
/// player more of its own would otherwise see its own composers overtake the room's, which travel
/// the room stream. <see cref="None"/> holds nothing.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct OutboundHold(
    [property: Id(0)] PlayerId PlayerId,
    [property: Id(1)] long Token
)
{
    public static OutboundHold None => default;

    public bool IsNone => Token == 0;
}
