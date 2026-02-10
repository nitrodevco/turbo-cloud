using System;
using Turbo.Primitives.Players;
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int RespectTotal { get; set; } = 0;
    public int RespectLeft { get; set; } = 0;
    public int PetRespectLeft { get; set; } = 0;
    public int RespectReplenishesLeft { get; set; } = 0;
    public DateTime LastRespectReset { get; set; } = DateTime.MinValue;
}
