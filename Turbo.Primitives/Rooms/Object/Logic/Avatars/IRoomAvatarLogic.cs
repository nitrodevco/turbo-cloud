using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Object.Logic.Avatars;

public interface IRoomAvatarLogic<out TObject, out TLogic, out TContext>
    : IRoomObjectLogic<TObject, TLogic, TContext>,
        IRoomAvatarLogic
    where TObject : IRoomAvatar<TObject, TLogic, TContext>
    where TContext : IRoomAvatarContext<TObject, TLogic, TContext>
    where TLogic : IRoomAvatarLogic<TObject, TLogic, TContext>
{
    new TContext Context { get; }
}

public interface IRoomAvatarLogic : IRoomObjectLogic, IRollableObject { }
