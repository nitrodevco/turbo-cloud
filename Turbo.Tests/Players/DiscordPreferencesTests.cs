using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Preferences;
using Turbo.Players.Grains.Settings;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The Discord settings window (WIN63-202610131318 <c>discord/settings/DiscordSettingsController.as</c>):
/// the client asks for the preferences at start and sends the four boxes with the settings
/// version when the window closes. Official client capture: a new account is answered
/// 0000000000000000, and after a save of version 1 with every box ticked the next login is
/// answered 0000000101010101; nothing answers the save itself.
/// </summary>
public sealed class DiscordPreferencesTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DiscordPreferencesTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_new_account_is_answered_version_0_and_four_falses_byte_for_byte()
    {
        var reply = await GetAsync(await SettingsAsync());

        reply.Should().Equal(Convert.FromHexString("0000000000000000"));
    }

    [Fact]
    public async Task A_save_is_kept_and_answered_as_saved_at_the_next_login()
    {
        var settings = await SettingsAsync();

        await SetAsync(settings, Convert.FromHexString("0000000101010101"));
        _fakes.Log.Of("SendComposerAsync").Should().BeEmpty("nothing answers the save");
        await ((Grain)settings).OnDeactivateAsync(new(DeactivationReasonCode.None, ""), Ct);

        var reply = await GetAsync(await SettingsAsync());

        reply.Should().Equal(Convert.FromHexString("0000000101010101"));
    }

    [Fact]
    public async Task Each_box_is_kept_apart()
    {
        var settings = await SettingsAsync();

        await SetAsync(settings, Convert.FromHexString("0000000101000100"));

        var saved = await settings.GetSettingsAsync(Ct);
        (
            saved.DiscordPreferencesVersion,
            saved.DiscordShowHabbo,
            saved.DiscordShareActivity,
            saved.DiscordHideInHiddenRooms,
            saved.DiscordAllowJoining
        )
            .Should()
            .Be((1, true, false, true, false));
    }

    [Fact]
    public async Task A_save_of_version_0_is_refused()
    {
        var settings = await SettingsAsync();

        await SetAsync(settings, Convert.FromHexString("0000000001010101"));

        (await settings.GetSettingsAsync(Ct)).DiscordPreferencesVersion.Should().Be(0);
        (await settings.GetSettingsAsync(Ct)).DiscordShowHabbo.Should().BeFalse();
    }

    private async Task<byte[]> GetAsync(IPlayerSettingsGrain settings)
    {
        _fakes.Instances[(typeof(IPlayerSettingsGrain), (object)(long)PLAYER)] = settings;
        while (_fakes.Log.Calls.TryDequeue(out _)) { }

        var message = PacketHarness.Parse(
            PacketHarness.Incoming("GetDiscordPreferencesMessageEvent"),
            []
        );
        await new GetDiscordPreferencesMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            (GetDiscordPreferencesMessage)message,
            Context(),
            Ct
        );

        var packet = PacketHarness.Encode(
            Assert.IsType<DiscordPreferencesMessageComposer>(
                _fakes.Log.Of("SendComposerAsync").Single().Args[0]
            )
        );

        return packet.PopBytes(packet.Remaining);
    }

    private async Task SetAsync(IPlayerSettingsGrain settings, byte[] payload)
    {
        _fakes.Instances[(typeof(IPlayerSettingsGrain), (object)(long)PLAYER)] = settings;
        while (_fakes.Log.Calls.TryDequeue(out _)) { }

        var message = PacketHarness.Parse(
            PacketHarness.Incoming("SetDiscordPreferencesMessageEvent"),
            payload
        );
        await new SetDiscordPreferencesMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            (SetDiscordPreferencesMessage)message,
            Context(),
            Ct
        );
    }

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), PLAYER, -1);

    private async Task<IPlayerSettingsGrain> SettingsAsync()
    {
        var grain = GrainHarness.Create<PlayerSettingsGrain>(_fakes, _db, PLAYER);
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerSettingsGrain)grain;
    }
}
