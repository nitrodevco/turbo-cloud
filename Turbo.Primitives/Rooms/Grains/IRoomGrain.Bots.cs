using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Takes a bot out of the acting player's inventory and stands it on a tile. (0, 0) means
    /// any free tile. Failures are reported to the player as a <c>BotError</c>.
    /// </summary>
    public Task<bool> PlaceBotAsync(
        ActionContext ctx,
        int botId,
        int x,
        int y,
        CancellationToken ct
    );

    /// <summary>Moves or turns a placed bot by its room object id, as <c>MoveEntityInFlat</c> names it.</summary>
    public Task<bool> MoveBotAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int x,
        int y,
        Rotation rotation,
        CancellationToken ct
    );
    public Task<bool> PickupBotAsync(ActionContext ctx, int botId, CancellationToken ct);

    /// <summary>Uses one of the bot's skills with the data the client's editor composed.</summary>
    public Task<bool> CommandBotAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        string data,
        CancellationToken ct
    );

    /// <summary>Answers a skill editor opening with the bot's current configuration.</summary>
    public Task<bool> RequestBotCommandConfigurationAsync(
        ActionContext ctx,
        int botId,
        BotSkillType skill,
        CancellationToken ct
    );
}
