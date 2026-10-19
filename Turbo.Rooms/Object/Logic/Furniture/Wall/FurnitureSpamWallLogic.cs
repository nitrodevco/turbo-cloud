using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Wall;

/// <summary>
/// A post-it wall. While one is in the room, a player who may not build here can still stick
/// notes from their own post-it pads on its walls: their placement is answered with the note
/// editor (<c>RequestSpamWallPostIt</c>, naming their pad and the spot) instead of a refusal, and
/// the note comes back as <c>AddSpamWallPostIt</c> (<see cref="Grains.Modules.RoomActionModule"/>,
/// the post-it part). Each note uses a sheet of that pad and belongs to the room's owner.
/// The wall itself does nothing more: a double-click is an ordinary use. Flash has no room logic
/// for it; its <c>SpamWallPostItWidgetHandler</c> looks the request's id up among the user's
/// inventory wall items to take the note's type, so the id is a pad's, never the wall's.
/// </summary>
[RoomObjectLogic("spam_wall")]
public class FurnitureSpamWallLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureWallLogic(stuffDataFactory, ctx) { }
