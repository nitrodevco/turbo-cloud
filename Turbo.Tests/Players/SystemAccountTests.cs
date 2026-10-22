using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Players.Grains;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A system or service account (<c>players.is_system_account</c>): an account the hotel or a plugin
/// acts as. The directory says which players are, and the friend search finds none of them. Before,
/// a hotel's service accounts were found and befriended as people.
/// </summary>
public sealed class SystemAccountTests : IDisposable
{
    private const int SEARCHER = 1;
    private const int PERSON = 2;
    private const int SERVICE = 3;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public SystemAccountTests()
    {
        Player(SEARCHER, "searcher");
        Player(PERSON, "frankie");
        Player(SERVICE, "frank");
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_directory_says_who_is_a_system_account_from_the_moment_it_is_marked()
    {
        var directory = Directory();

        (await directory.GetSystemAccountIdsAsync(Ct)).Should().BeEmpty();

        (await directory.SetSystemAccountAsync(SERVICE, true, Ct)).Should().BeTrue();
        (await directory.GetSystemAccountIdsAsync(Ct)).Should().Equal((PlayerId)SERVICE);

        (await directory.SetSystemAccountAsync(SERVICE, false, Ct)).Should().BeTrue();
        (await directory.GetSystemAccountIdsAsync(Ct)).Should().BeEmpty();

        (await directory.SetSystemAccountAsync(99, true, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task The_friend_search_does_not_find_a_system_account()
    {
        await Directory().SetSystemAccountAsync(SERVICE, true, Ct);

        var messenger = (IPlayerMessengerGrain)
            GrainHarness.Create(
                typeof(PlayerDirectoryGrain).Assembly,
                "Turbo.Players.Grains.Messenger.PlayerMessengerGrain",
                new Fakes(),
                _db,
                SEARCHER
            );

        var (friends, others) = await messenger.SearchPlayersAsync("frank", Ct);

        friends.Should().BeEmpty();
        others.Select(x => x.Name).Should().Equal("frankie");
    }

    private IPlayerDirectoryGrain Directory() =>
        GrainHarness.Create<PlayerDirectoryGrain>(new Fakes(), _db);

    private void Player(int id, string name) =>
        _db.Insert(
            new PlayerEntity
            {
                Id = id,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
}
