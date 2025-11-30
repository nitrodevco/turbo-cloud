using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Mapping;

public interface IRoomMapViewer
{
    public int Width { get; }
    public int Height { get; }
    public int GetTileId(int x, int y);
    public bool CanAvatarWalk(
        IRoomAvatar avatar,
        int tileId,
        bool isGoal = true,
        bool isDiagonalCheck = false
    );
    public bool CanAvatarWalkBetween(
        IRoomAvatar avatar,
        int fromTileId,
        int toTileId,
        bool isGoal = true
    );
}
