using Orleans;
using Orleans.Runtime;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans;

/// <summary>
/// Reads a grain's own key as the identifier it stands for, so grains do not repeat the cast from
/// the Orleans primary key.
/// </summary>
public static class GrainKeyExtensions
{
    public static PlayerId GetPlayerId(this IAddressable grain) =>
        PlayerId.Parse((int)grain.GetPrimaryKeyLong());

    public static RoomId GetRoomId(this IAddressable grain) =>
        RoomId.Parse((int)grain.GetPrimaryKeyLong());
}
