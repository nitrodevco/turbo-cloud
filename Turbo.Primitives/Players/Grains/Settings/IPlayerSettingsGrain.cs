using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Settings;

namespace Turbo.Primitives.Players.Grains.Settings;

public interface IPlayerSettingsGrain : IGrainWithIntegerKey
{
    public Task<PlayerSettingsSnapshot> GetSettingsAsync(CancellationToken ct);
    public Task<bool> SetSoundSettingsAsync(
        int genericVolume,
        int furniVolume,
        int traxVolume,
        CancellationToken ct
    );
    public Task<bool> SetChatPreferencesAsync(
        ChatModeType chatMode,
        ChatBubbleWidthType bubbleWidth,
        ChatScrollSpeedType scrollSpeed,
        CancellationToken ct
    );
    public Task<bool> SetChatStyleAsync(
        int chatStyleId,
        ChatSizeType fontSize,
        CancellationToken ct
    );
    public Task<bool> SetIgnoreRoomInvitesAsync(bool ignoreRoomInvites, CancellationToken ct);
    public Task<bool> SetRoomCameraFollowDisabledAsync(
        bool cameraFollowDisabled,
        CancellationToken ct
    );
    public Task<bool> SetUIFlagsAsync(UIFlags uiFlags, CancellationToken ct);
    public Task<bool> SetWiredPreferencesAsync(
        bool menuButton,
        bool inspectButton,
        bool playTestMode,
        int variableSyntaxMode,
        bool whisperDisabled,
        bool showAllNotifications,
        string uiStyle,
        CancellationToken ct
    );
}
