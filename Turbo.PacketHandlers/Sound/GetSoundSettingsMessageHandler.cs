using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// Habbo answers GetSoundSettings with AccountPreferences: the volumes and every other account
/// preference the client keeps (chat mode, UI flags, wired options).
/// </summary>
public class GetSoundSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetSoundSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSoundSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var settings = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetSettingsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new AccountPreferencesEventMessageComposer
                {
                    GenericVolume = settings.GenericVolume,
                    FurniVolume = settings.FurniVolume,
                    TraxVolume = settings.TraxVolume,
                    FreeFlowChatDisabled = settings.ChatMode == ChatModeType.Old,
                    RoomInvitesIgnored = settings.RoomInvitesIgnored,
                    RoomCameraFollowDisabled = settings.RoomCameraFollowDisabled,
                    UIFlags = settings.UIFlags,
                    PreferedChatStyle = settings.ChatStyleId,
                    WiredMenuButton = settings.WiredMenuButton,
                    WiredInspectButton = settings.WiredInspectButton,
                    PlayTestMode = settings.WiredPlayTestMode,
                    VariableSyntaxMode = settings.WiredVariableSyntaxMode,
                    WiredWhisperDisabled = settings.WiredWhisperDisabled,
                    ShowAllNotifications = settings.WiredShowAllNotifications,
                    WiredUIStyle = settings.WiredUIStyle,
                    ChatSizePreference = settings.ChatFontSize,
                    ChatMode = settings.ChatMode,
                    ChatBubbleWidth = settings.ChatBubbleWidth,
                    ChatScrollSpeed = settings.ChatScrollSpeed,
                    OnlineIndicatorPreference = settings.OnlineIndicatorPreference,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
