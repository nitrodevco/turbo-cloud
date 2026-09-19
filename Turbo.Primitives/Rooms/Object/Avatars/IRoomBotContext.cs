using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomBotContext : IRoomAvatarContext<IRoomBot, IRoomBotLogic, IRoomBotContext>
{
    new IRoomBot RoomObject { get; }
}
