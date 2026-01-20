using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayer : IRoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>
{
    public PlayerId PlayerId { get; }
}
