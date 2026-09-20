using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.PacketHandlers.Users;

public class ScrGetUserInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ScrGetUserInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ScrGetUserInfoMessage message,
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

        await _grainFactory
            .GetPlayerSubscriptionGrain(ctx.PlayerId)
            .SendClubInfoAsync(ScrUserInfoResponseType.Normal, ct)
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
