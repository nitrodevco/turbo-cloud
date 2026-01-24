using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object;

public abstract class RoomObjectContext<TObject, TLogic, TSelf>(
    RoomGrain roomGrain,
    TObject roomObject
) : IRoomObjectContext<TObject, TLogic, TSelf>
    where TObject : IRoomObject<TObject, TLogic, TSelf>
    where TSelf : IRoomObjectContext<TObject, TLogic, TSelf>
    where TLogic : IRoomObjectLogic<TObject, TLogic, TSelf>
{
    protected readonly RoomGrain _roomGrain = roomGrain;
    protected readonly TObject _roomObject = roomObject;

    public RoomId RoomId => _roomGrain._state.RoomId;
    public IRoomGrain Room => _roomGrain;

    public RoomObjectId ObjectId => _roomObject.ObjectId;
    public TObject RoomObject => _roomObject;

    IRoomObject IRoomObjectContext.RoomObject => RoomObject;
}
