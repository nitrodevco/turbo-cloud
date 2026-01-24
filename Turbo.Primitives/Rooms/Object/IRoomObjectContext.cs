using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Primitives.Rooms.Object;

public interface IRoomObjectContext<out TObject, out TLogic, out TSelf> : IRoomObjectContext
    where TObject : IRoomObject<TObject, TLogic, TSelf>
    where TSelf : IRoomObjectContext<TObject, TLogic, TSelf>
    where TLogic : IRoomObjectLogic<TObject, TLogic, TSelf>
{
    new TObject RoomObject { get; }
}

public interface IRoomObjectContext
{
    public RoomId RoomId { get; }
    public IRoomGrain Room { get; }

    public RoomObjectId ObjectId { get; }
    public IRoomObject RoomObject { get; }
}
