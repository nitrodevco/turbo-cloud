using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelBots"/> over the room grains' bot orders. A room no one is in is not loaded
/// for a bot (the room directory says who is where without loading it).
/// </summary>
internal sealed class HostHotelBots(IGrainFactory grains) : IHotelBots
{
    public async Task<IReadOnlyList<HotelBot>> InRoomAsync(RoomId room, CancellationToken ct)
    {
        if (!await IsOccupiedAsync(room, ct).ConfigureAwait(false))
            return [];

        var bots = await grains.GetRoomGrain(room).GetPlacedBotsAsync(ct).ConfigureAwait(false);

        return
        [
            .. bots.Select(x => new HotelBot(
                x.Id,
                x.Name,
                x.Motto,
                x.OwnerId,
                x.OwnerName,
                room,
                x.X,
                x.Y
            )),
        ];
    }

    public Task<bool> SayAsync(
        RoomId room,
        int botId,
        string text,
        bool shout,
        CancellationToken ct
    ) => OrderAsync(room, botId, new BotSayOrder(text, shout), ct);

    public Task<bool> WhisperAsync(
        RoomId room,
        int botId,
        PlayerId player,
        string text,
        CancellationToken ct
    ) => OrderAsync(room, botId, new BotWhisperOrder(player, text), ct);

    public Task<bool> WalkToAsync(RoomId room, int botId, int x, int y, CancellationToken ct) =>
        OrderAsync(room, botId, new BotWalkOrder(x, y), ct);

    public Task<bool> WalkToFurniAsync(RoomId room, int botId, int itemId, CancellationToken ct) =>
        OrderAsync(room, botId, new BotWalkToItemOrder(itemId), ct);

    public Task<bool> GestureAsync(
        RoomId room,
        int botId,
        HotelBotGesture gesture,
        CancellationToken ct
    ) => OrderAsync(room, botId, new BotGestureOrder(ToExpression(gesture)), ct);

    public Task<bool> DanceAsync(RoomId room, int botId, int dance, CancellationToken ct) =>
        dance is < 0 or > 4
            ? Task.FromResult(false)
            : OrderAsync(room, botId, new BotDanceOrder((AvatarDanceType)dance), ct);

    public Task<bool> SignAsync(RoomId room, int botId, int sign, CancellationToken ct) =>
        OrderAsync(room, botId, new BotSignOrder(sign), ct);

    internal static AvatarExpressionType ToExpression(HotelBotGesture gesture) =>
        gesture switch
        {
            HotelBotGesture.Wave => AvatarExpressionType.Wave,
            HotelBotGesture.BlowKiss => AvatarExpressionType.Blow,
            HotelBotGesture.Laugh => AvatarExpressionType.Laugh,
            HotelBotGesture.Cry => AvatarExpressionType.Cry,
            HotelBotGesture.Jump => AvatarExpressionType.Jump,
            _ => AvatarExpressionType.None,
        };

    private async Task<bool> OrderAsync(
        RoomId room,
        int botId,
        BotOrder order,
        CancellationToken ct
    ) =>
        await IsOccupiedAsync(room, ct).ConfigureAwait(false)
        && await grains.GetRoomGrain(room).OrderBotAsync(botId, order, ct).ConfigureAwait(false);

    private async Task<bool> IsOccupiedAsync(RoomId room, CancellationToken ct) =>
        await grains.GetRoomDirectoryGrain().GetRoomPopulationAsync(room, ct).ConfigureAwait(false)
        > 0;
}
