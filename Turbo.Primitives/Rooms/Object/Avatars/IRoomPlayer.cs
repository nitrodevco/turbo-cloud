using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayer : IRoomAvatar<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>
{
    new IRoomPlayerLogic Logic { get; }
    public PlayerId PlayerId { get; }
    public AvatarGenderType Gender { get; }
    public AvatarDanceType DanceType { get; }
    public int EffectId { get; }
    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot);
    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None);
    public bool SetEffect(int effectId = 0);
}
