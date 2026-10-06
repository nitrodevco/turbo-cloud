using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Players.Accounts;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Creating a player: a name the client and the commands can use, free whatever its case; a motto
/// and figure of the sizes the hotel keeps; and the gender's own figure when none is given.
/// </summary>
public sealed class PlayerAccountServiceTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly PlayerAccountService _accounts;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerAccountServiceTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "Alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Female,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _accounts = new PlayerAccountService(
            _db,
            new NoOwner(),
            NullLogger<IPlayerAccountService>.Instance
        );
    }

    private sealed class NoOwner : IOwnerBootstrap
    {
        public Task<bool> PlayerCreatedAsync(PlayerId player, string name, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<bool> DiscordLinkedAsync(
            PlayerId player,
            string discordId,
            CancellationToken ct
        ) => Task.FromResult(false);
    }

    public void Dispose() => _db.Dispose();

    private async Task<PlayerEntity> ReadAsync(int id)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.Players.SingleAsync(x => x.Id == id, Ct);
    }

    [Fact]
    public async Task ANewPlayer_IsSaved_WithTheirGendersFigure()
    {
        var result = await _accounts.CreateAsync(
            new NewPlayer(" Bob ", "Hello", AvatarGenderType.Female, null),
            Ct
        );

        result.Error.Should().BeNull();

        var bob = await ReadAsync(result.Created!.Value.Value);

        bob.Name.Should().Be("Bob");
        bob.Motto.Should().Be("Hello");
        bob.Gender.Should().Be(AvatarGenderType.Female);
        bob.Figure.Should().Be(PlayerAccountService.FEMALE_FIGURE);
        bob.PlayerStatus.Should().Be(PlayerStatusType.Offline);
    }

    [Fact]
    public async Task AGivenFigure_IsKept()
    {
        var result = await _accounts.CreateAsync(
            new NewPlayer("Carl", null, AvatarGenderType.Male, "hd-190-1.ch-210-66"),
            Ct
        );

        (await ReadAsync(result.Created!.Value.Value)).Figure.Should().Be("hd-190-1.ch-210-66");
    }

    [Theory]
    [InlineData("ALICE", "already a player")]
    [InlineData("al", "3 to 15")]
    [InlineData("a_very_long_name_1", "3 to 15")]
    [InlineData("two words", "no spaces")]
    [InlineData("@room", "start with @")]
    [InlineData("émile", "letters, digits")]
    public async Task ANameTheHotelCannotUse_IsRefused(string name, string why)
    {
        var result = await _accounts.CreateAsync(
            new NewPlayer(name, null, AvatarGenderType.Male, null),
            Ct
        );

        result.Created.Should().BeNull();
        result.Error.Should().Contain(why);
    }

    [Fact]
    public async Task AMottoLongerThanTheClientShows_IsRefused() =>
        (
            await _accounts.CreateAsync(
                new NewPlayer("Dana", new string('x', 39), AvatarGenderType.Male, null),
                Ct
            )
        )
            .Error.Should()
            .Contain("motto");
}
