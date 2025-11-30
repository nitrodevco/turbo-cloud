using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Object.Logic.Avatars;

public interface IRoomAvatarLogic : IRoomObjectLogic
{
    public IRoomAvatarContext Context { get; }
}
