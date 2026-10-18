using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Sound;
using Turbo.PacketHandlers.Users;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Which request the account preferences answer (recorded on hhus, 2026-10-12, each request sent
/// alone): GetSoundSettings gets AccountPreferences; ScrGetUserInfo gets the club info only.
/// </summary>
public sealed class AccountPreferencesRequestTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AccountPreferencesRequestTests() =>
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSettingsSnapshot>());

    [Fact]
    public async Task sound_settings_are_answered_with_the_account_preferences()
    {
        await new GetSoundSettingsMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new GetSoundSettingsMessage(),
            Context(),
            Ct
        );

        Assert.IsType<AccountPreferencesEventMessageComposer>(
            _fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
    }

    // Flash reads the boolean after the volumes and discards it (AccountPreferencesParser.parse); the
    // line-by-line chat mode is the chatMode int, and the official preferences carry chatMode 1 with
    // that boolean false. Sent true, our client hid the room chat.
    [Fact]
    public async Task line_by_line_chat_mode_does_not_set_the_discarded_boolean()
    {
        _fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(
                Activator.CreateInstance<PlayerSettingsSnapshot>() with
                {
                    ChatMode = ChatModeType.Old,
                }
            );

        await new GetSoundSettingsMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new GetSoundSettingsMessage(),
            Context(),
            Ct
        );

        var sent = Assert.IsType<AccountPreferencesEventMessageComposer>(
            _fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        Assert.Equal(ChatModeType.Old, sent.ChatMode);
        Assert.False(sent.FreeFlowChatDisabled);
    }

    [Fact]
    public async Task club_info_does_not_resend_the_account_preferences()
    {
        await new ScrGetUserInfoMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new ScrGetUserInfoMessage { ProductName = "habbo_club" },
            Context(),
            Ct
        );

        Assert.DoesNotContain(
            _fakes.Log.Of("SendComposerAsync"),
            x => x.Args[0] is AccountPreferencesEventMessageComposer
        );
    }

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, -1);
}
