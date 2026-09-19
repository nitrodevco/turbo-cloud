using System;
using System.Collections.Immutable;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Grains;

public sealed class PlayerLiveState
{
    public required PlayerId PlayerId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Motto { get; set; } = string.Empty;
    public string Figure { get; set; } = string.Empty;
    public AvatarGenderType Gender { get; set; } = AvatarGenderType.Male;
    public int AchievementScore { get; set; } = 0;
    public int BadgesRank { get; set; } = 0;
    public bool IsOnline { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int RespectPoints { get; set; } = 0;
    public int RespectsLeft { get; set; } = 0;
    public int PetRespectsLeft { get; set; } = 0;
    public int RespectReplenishesLeft { get; set; } = 0;
    public DateTime? RespectResetDate { get; set; } = null;
    public PlayerPerkFlags Perks { get; set; } = PlayerPerkFlags.None;
    public ImmutableArray<PlayerBadgeSnapshot>? SelectedBadges { get; set; } = null;
}
