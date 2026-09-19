using System.Collections.Immutable;
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

    /// <summary>Codes of the badges the player wears, loaded when the avatar enters.</summary>
    public ImmutableArray<string> BadgeCodes { get; }
    public bool UpdateWithPlayer(PlayerSummarySnapshot snapshot);
    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None);
    public bool SetEffect(int effectId = 0);
    public void SetBadges(ImmutableArray<string> badgeCodes);
}
