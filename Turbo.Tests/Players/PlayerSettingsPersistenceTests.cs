using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Settings;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A player's settings are written when the grain flushes and read back when it next activates.
/// A player who has never changed anything has no row, so the first change creates it.
/// </summary>
public sealed class PlayerSettingsPersistenceTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerSettingsPersistenceTests() =>
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

    /// <summary>
    /// Muting is 0, the CLR default of a volume, and a 0 left out of the insert that creates the
    /// row was stored as the column's default of 100: the sound was back on at the next login.
    /// </summary>
    [Fact]
    public async Task Volumes_muted_as_the_first_change_stay_muted_at_the_next_login()
    {
        var first = await SettingsAsync();
        await first.SetSoundSettingsAsync(0, 0, 0, Ct);
        await ((Grain)first).OnDeactivateAsync(new(DeactivationReasonCode.None, ""), Ct);

        var settings = await (await SettingsAsync()).GetSettingsAsync(Ct);

        (settings.GenericVolume, settings.FurniVolume, settings.TraxVolume).Should().Be((0, 0, 0));
    }

    private async Task<IPlayerSettingsGrain> SettingsAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Settings.PlayerSettingsGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerSettingsGrain)grain;
    }
}
