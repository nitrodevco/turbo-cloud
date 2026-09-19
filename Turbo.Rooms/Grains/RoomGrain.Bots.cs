using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> PlaceBotAsync(
        ActionContext ctx,
        int botId,
        int x,
        int y,
        CancellationToken ct
    ) =>
        RunBotActionAsync(ctx, botId, "place", () => BotModule.PlaceBotAsync(ctx, botId, x, y, ct));

    public Task<bool> MoveBotAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    ) =>
        RunBotActionAsync(
            ctx,
            objectId,
            "move",
            () => BotModule.MoveBotAsync(ctx, objectId, x, y, rotation, ct)
        );

    public Task<bool> PickupBotAsync(ActionContext ctx, int botId, CancellationToken ct) =>
        RunBotActionAsync(ctx, botId, "pick up", () => BotModule.PickupBotAsync(ctx, botId, ct));

    public Task<bool> CommandBotAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        string data,
        CancellationToken ct
    ) =>
        RunBotActionAsync(
            ctx,
            botId,
            "command",
            () => BotModule.CommandBotAsync(ctx, botId, skill, data, ct)
        );

    public Task<bool> RequestBotCommandConfigurationAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        CancellationToken ct
    ) =>
        RunBotActionAsync(
            ctx,
            botId,
            "get the configuration of",
            () => BotModule.RequestConfigurationAsync(ctx, botId, skill, ct)
        );

    private async Task<bool> RunBotActionAsync(
        ActionContext ctx,
        int botId,
        string action,
        Func<Task<bool>> body
    )
    {
        try
        {
            AvatarModule.TouchAvatar(ctx.PlayerId, NowMs());

            return await body();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to {Action} bot {BotId} in room {RoomId}",
                ctx.PlayerId,
                action,
                botId,
                _state.RoomId
            );

            return false;
        }
    }
}
