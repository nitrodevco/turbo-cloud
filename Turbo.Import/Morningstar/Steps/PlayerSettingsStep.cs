using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The preferences in <c>users_settings</c> that Turbo keeps in <c>player_settings</c>: sound
/// volumes, the privacy switches, the chat bubble and old-style chat, the client's UI flags and
/// the home room (after the rooms, so a home room that was not copied is dropped). A player whose
/// settings are all Turbo's defaults gets no row, as a new player has none.
/// </summary>
internal sealed class PlayerSettingsStep : IImportStep
{
    public string Name => "player settings";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerSettingsEntity>("player_settings");

        foreach (var (player, settings) in ctx.UserSettings)
        {
            if (!ctx.Players.Contains(player))
                continue;

            var home =
                settings.HomeRoom > 0 && ctx.RoomOwners.ContainsKey(settings.HomeRoom)
                    ? settings.HomeRoom
                    : (int?)null;

            var entity = new PlayerSettingsEntity
            {
                CreatedAt = ctx.Now,
                PlayerEntityId = player,
                GenericVolume = Volume(settings.VolumeSystem),
                FurniVolume = Volume(settings.VolumeFurni),
                TraxVolume = Volume(settings.VolumeTrax),
                RoomInvitesIgnored = settings.BlockRoomInvites,
                FriendRequestsDisabled = settings.BlockFriendRequests,
                FollowingDisabled = settings.BlockFollowing,
                RoomCameraFollowDisabled = settings.BlockCameraFollow,
                ChatStyleId = Math.Max(0, settings.ChatColor),
                ChatMode = settings.OldChat ? ChatModeType.Old : ChatModeType.FreeFlow,
                HomeRoomId = home,
            };

            // ui_flags is the client's own bit set; only the bits Turbo knows are kept.
            if (settings.UiFlags >= 0)
                entity.UIFlags =
                    (UIFlags)settings.UiFlags
                    & (UIFlags.FriendBarExpanded | UIFlags.RoomToolsExpanded);

            if (IsDefault(entity))
                continue;

            await batch.AddAsync(entity, ct).ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static int Volume(int value) =>
        Math.Clamp(value, PlayerSettingsEntity.VOLUME_MIN, PlayerSettingsEntity.VOLUME_MAX);

    private static bool IsDefault(PlayerSettingsEntity s) =>
        s.GenericVolume == PlayerSettingsEntity.DEFAULT_VOLUME
        && s.FurniVolume == PlayerSettingsEntity.DEFAULT_VOLUME
        && s.TraxVolume == PlayerSettingsEntity.DEFAULT_VOLUME
        && !s.RoomInvitesIgnored
        && !s.FriendRequestsDisabled
        && !s.FollowingDisabled
        && !s.RoomCameraFollowDisabled
        && s.ChatStyleId == PlayerSettingsEntity.DEFAULT_CHAT_STYLE_ID
        && s.ChatMode == PlayerSettingsEntity.DEFAULT_CHAT_MODE
        && s.UIFlags == PlayerSettingsEntity.DEFAULT_UI_FLAGS
        && s.HomeRoomId is null;
}
