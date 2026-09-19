using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Object.Logic.Avatar;

/// <summary>Behaviour runs in <c>RoomBotTickSystem</c>; the logic carries the object lifecycle.</summary>
[RoomObjectLogic("default_bot")]
public sealed class BotLogic(IRoomBotContext ctx)
    : AvatarLogic<IRoomBot, IRoomBotLogic, IRoomBotContext>(ctx),
        IRoomBotLogic { }
