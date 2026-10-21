using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Admin.Rooms;

/// <summary>
/// The bots standing in a room, for its page. A bot in a room is the room's: it is set, or taken
/// back to its owner's inventory, through the room grain, so those in the room see the change and
/// the room refuses a bot that is not standing in it.
/// </summary>
public sealed class AdminRoomBots(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
)
{
    /// <summary>The bots standing in the room, by name.</summary>
    public async Task<List<RoomBotItem>> ListAsync(int roomId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .Bots.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new RoomBotItem(
                x.Id,
                x.Name,
                x.Motto,
                x.Figure,
                x.Gender,
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.ChatText ?? "",
                x.AutoChat,
                x.ChatDelaySeconds,
                x.MixSentences,
                x.FreeRoam,
                x.DanceType
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a bot standing in the room; false when it doesn't stand there, or its name is too
    /// short or too long.
    /// </summary>
    public Task<bool> UpdateAsync(
        int roomId,
        int botId,
        RoomBotRequest request,
        CancellationToken ct
    ) =>
        Room(roomId)
            .StaffUpdateBotAsync(
                botId,
                new BotStaffEditSnapshot
                {
                    Name = request.Name ?? string.Empty,
                    Motto = request.Motto ?? string.Empty,
                    Figure = request.Figure ?? string.Empty,
                    Gender = request.Gender ?? AvatarGenderType.Male,
                    ChatText = request.ChatText ?? string.Empty,
                    AutoChat = request.AutoChat ?? false,
                    ChatDelaySeconds = request.ChatDelaySeconds ?? 0,
                    MixSentences = request.MixSentences ?? false,
                    FreeRoam = request.FreeRoam ?? false,
                    Dance = request.Dance ?? AvatarDanceType.None,
                },
                ct
            );

    /// <summary>Takes a bot out of the room, back to its owner's inventory; false when it doesn't stand there.</summary>
    public Task<bool> PickupAsync(int roomId, int botId, CancellationToken ct) =>
        Room(roomId).StaffPickupBotAsync(botId, ct);

    private IRoomGrain Room(int roomId) => grainFactory.GetRoomGrain(new RoomId(roomId));
}
