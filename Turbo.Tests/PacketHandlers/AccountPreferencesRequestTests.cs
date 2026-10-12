using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Sound;
using Turbo.PacketHandlers.Users;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Preferences;
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
