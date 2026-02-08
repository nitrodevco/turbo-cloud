using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayer : IRoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>
{
    new IRoomPlayerLogic Logic { get; }
    public PlayerId PlayerId { get; }
    public AvatarGenderType Gender { get; }
    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot);
}
