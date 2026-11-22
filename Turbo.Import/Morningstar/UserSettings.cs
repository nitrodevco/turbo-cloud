using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Import.Source;

namespace Turbo.Import.Morningstar;

/// <summary>
/// The part of a player's <c>users_settings</c> row the import copies, read once and kept by user
/// id: the players step and the settings step both need it, and the table is one row a player.
/// </summary>
internal sealed record UserSettings
{
    public int RespectsReceived { get; init; }
    public int DailyRespects { get; init; }
    public int DailyPetRespects { get; init; }
    public int HomeRoom { get; init; }
    public int VolumeSystem { get; init; } = 100;
    public int VolumeFurni { get; init; } = 100;
    public int VolumeTrax { get; init; } = 100;
    public bool BlockFollowing { get; init; }
    public bool BlockFriendRequests { get; init; }
    public bool BlockRoomInvites { get; init; }
    public bool BlockCameraFollow { get; init; }
    public bool OldChat { get; init; }
    public int ChatColor { get; init; }
    public int UiFlags { get; init; }
    public DateTime? ClubExpires { get; init; }
    public int OnlineSeconds { get; init; }
    public int FavouriteGuild { get; init; }

    public static async Task<Dictionary<int, UserSettings>> ReadAsync(
        SourceDatabase source,
        CancellationToken ct
    )
    {
        var settings = new Dictionary<int, UserSettings>();

        await foreach (
            var row in source.ReadAsync("users_settings", "user_id", ct).ConfigureAwait(false)
        )
            settings[row.Int("user_id")] = new UserSettings
            {
                RespectsReceived = row.Int("respects_received"),
                DailyRespects = row.Int("daily_respect_points", 3),
                DailyPetRespects = row.Int("daily_pet_respect_points", 3),
                HomeRoom = row.Int("home_room"),
                VolumeSystem = row.Int("volume_system", 100),
                VolumeFurni = row.Int("volume_furni", 100),
                VolumeTrax = row.Int("volume_trax", 100),
                BlockFollowing = row.Bool("block_following"),
                BlockFriendRequests = row.Bool("block_friendrequests"),
                BlockRoomInvites = row.Bool("block_roominvites"),
                BlockCameraFollow = row.Bool("block_camera_follow"),
                OldChat = row.Bool("old_chat"),
                ChatColor = row.Int("chat_color"),
                UiFlags = row.Int("ui_flags", -1),
                ClubExpires = row.UnixTime("club_expire_timestamp"),
                OnlineSeconds = row.Int("online_time"),
                FavouriteGuild = row.Int("guild_id"),
            };

        return settings;
    }
}
