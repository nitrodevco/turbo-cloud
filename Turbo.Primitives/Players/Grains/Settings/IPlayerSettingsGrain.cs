using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Players.Grains.Settings;

/// <summary>
/// Owns a player's account preferences. Setters validate and log rejections themselves; the
/// client never expects a response, so callers have nothing to act on.
/// </summary>
public interface IPlayerSettingsGrain : IGrainWithIntegerKey
{
    public Task<PlayerSettingsSnapshot> GetSettingsAsync(CancellationToken ct);
    public Task SetSoundSettingsAsync(
        int genericVolume,
        int furniVolume,
        int traxVolume,
        CancellationToken ct
    );
    public Task SetChatPreferencesAsync(
        ChatModeType chatMode,
        ChatBubbleWidthType bubbleWidth,
        ChatScrollSpeedType scrollSpeed,
        CancellationToken ct
    );
    public Task SetChatStyleAsync(int chatStyleId, ChatSizeType fontSize, CancellationToken ct);
    public Task SetIgnoreRoomInvitesAsync(bool ignoreRoomInvites, CancellationToken ct);
    public Task SetRoomCameraFollowDisabledAsync(bool cameraFollowDisabled, CancellationToken ct);
    public Task SetUIFlagsAsync(UIFlags uiFlags, CancellationToken ct);
    public Task SetWiredPreferencesAsync(
        bool menuButton,
        bool inspectButton,
        bool playTestMode,
        int variableSyntaxMode,
        bool whisperDisabled,
        bool showAllNotifications,
        string uiStyle,
        CancellationToken ct
    );

    /// <summary>Sets the home room (a non-positive id clears it) and tells the player.</summary>
    public Task SetHomeRoomAsync(RoomId roomId, CancellationToken ct);
    public Task SetNavigatorWindowPreferencesAsync(
        int x,
        int y,
        int width,
        int height,
        bool leftPaneHidden,
        NavigatorViewModeType resultsMode,
        CancellationToken ct
    );
}
