using System.Threading;
using System.Threading.Tasks;
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
        RunLoggedAsync(
            ctx,
            "place bot",
            botId,
            () => BotModule.PlaceBotAsync(ctx, botId, x, y, ct)
        );

    public Task<bool> MoveBotAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "move bot",
            objectId,
            () => BotModule.MoveBotAsync(ctx, objectId, x, y, rotation, ct)
        );

    public Task<bool> PickupBotAsync(ActionContext ctx, int botId, CancellationToken ct) =>
        RunLoggedAsync(ctx, "pick up bot", botId, () => BotModule.PickupBotAsync(ctx, botId, ct));

    public Task<bool> CommandBotAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        string data,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "command bot",
            botId,
            () => BotModule.CommandBotAsync(ctx, botId, skill, data, ct)
        );

    public Task<bool> RequestBotCommandConfigurationAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        CancellationToken ct
    ) =>
        RunLoggedAsync(
            ctx,
            "get the configuration of bot",
            botId,
            () => BotModule.RequestConfigurationAsync(ctx, botId, skill, ct)
        );
}
