using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A badge's "Owned by N users" counts the players who hold it. The badge grain saves the new
/// row before it tells the directory, so a directory that counts the rows on activation (or on
/// its recount timer) in between has counted the grant already: adding one on top showed a
/// badge given to one player as owned by two.
/// </summary>
public sealed class BadgeDirectoryOwnerCountTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BadgeDirectoryOwnerCountTests()
    {
        for (var id = 1; id <= 2; id++)
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"player-{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_grant_the_directory_counted_on_activation_is_not_counted_twice()
    {
        Own(1, 1, "NEW1");
        var directory = await DirectoryAsync();

        var info = await directory.OnBadgeGrantedAsync("NEW1", Ct);

        info.OwnerCount.Should().Be(1);
    }

    [Fact]
    public async Task A_grant_after_activation_adds_its_owner()
    {
        Own(1, 1, "NEW1");
        var directory = await DirectoryAsync();
        Own(2, 2, "NEW1");

        var info = await directory.OnBadgeGrantedAsync("NEW1", Ct);

        info.OwnerCount.Should().Be(2);
    }

    [Fact]
    public async Task A_revoke_the_directory_counted_on_activation_is_not_taken_off_twice()
    {
        // Player 2's row is deleted before the directory counts; player 1 still holds the badge.
        Own(1, 1, "NEW1");
        var directory = await DirectoryAsync();

        await directory.OnBadgeRevokedAsync("NEW1", Ct);

        (await directory.GetInfoAsync(["NEW1"], Ct))[0].OwnerCount.Should().Be(1);
    }

    private async Task<IBadgeDirectoryGrain> DirectoryAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Badges.BadgeDirectoryGrain",
            _fakes,
            _db
        );
        await ((Grain)grain).OnActivateAsync(Ct);
        return (IBadgeDirectoryGrain)grain;
    }

    private void Own(int id, int playerId, string code) =>
        _db.Insert(
            new PlayerBadgeEntity
            {
                Id = id,
                PlayerEntityId = playerId,
                BadgeCode = code,
                PlayerEntity = null!,
            }
        );
}
