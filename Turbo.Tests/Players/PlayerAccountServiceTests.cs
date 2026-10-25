using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Players.Accounts;
using Turbo.Players.Configuration;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
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
    private readonly TestEventBus _events = new();
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
            new PassThroughWordFilter(),
            Options.Create(new OnboardingConfig()),
            new PlayerNamePolicy(
                Options.Create(new PlayerConfig { BlockedNames = ["Admin", "MOD-*"] })
            ),
            NullLogger<IPlayerAccountService>.Instance,
            _events.System
        );
        _events.Record<PlayerRegisteredEvent>();
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

    [Theory]
    [InlineData("admin")]
    [InlineData("MOD-Bob")]
    [InlineData("mod-x")]
    public async Task ANameTheHotelBlocks_IsRefused_WhateverItsCase(string name)
    {
        var result = await _accounts.CreateAsync(
            new NewPlayer(name, null, AvatarGenderType.Male, null),
            Ct
        );

        result.Error.Should().Be(PlayerNames.FILTERED);
    }

    [Theory]
    [InlineData("Carol", 1)]
    [InlineData("alice", 0)]
    public async Task A_new_player_raises_PlayerRegistered_once_and_a_refused_name_none(
        string name,
        int raised
    )
    {
        var result = await _accounts.CreateAsync(
            new NewPlayer(name, null, AvatarGenderType.Male, null),
            Ct
        );

        await _events.WhenPublishedAsync<PlayerRegisteredEvent>(raised);

        _events
            .Of<PlayerRegisteredEvent>()
            .Select(x => (x.PlayerId, x.Name))
            .Should()
            .Equal(raised == 0 ? [] : [(result.Created!.Value, name)]);
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

    /// <summary>
    /// A hotel caps accounts per address and checks the chosen
    /// look before the player exists, so the policy sees what would be saved and the address, a
    /// refusal makes nobody and says why, and both outcomes carry the address to their events.
    /// </summary>
    [Fact]
    public async Task A_registration_policy_decides_before_the_player_is_made_and_the_events_carry_the_address()
    {
        var asked = new List<Turbo.Primitives.Policies.RegistrationRequest>();
        var policies = Turbo.Players.Policies.GamePolicies.None;

        policies.Register("frontdoor", new OnePerAddress(asked));
        _events.Record<PlayerRegistrationFailedEvent>();

        var accounts = new PlayerAccountService(
            _db,
            new NoOwner(),
            new PassThroughWordFilter(),
            Options.Create(new OnboardingConfig()),
            new PlayerNamePolicy(Options.Create(new PlayerConfig())),
            NullLogger<IPlayerAccountService>.Instance,
            _events.System,
            policies
        );
        NewPlayer FromSite(string name) =>
            new(name, null, AvatarGenderType.Female, null)
            {
                Source = RegistrationSource.Site,
                RemoteAddress = "203.0.113.7",
            };

        var first = await accounts.CreateAsync(FromSite(" Erin "), Ct);
        var second = await accounts.CreateAsync(FromSite("Fay"), Ct);

        first.Created.Should().NotBeNull();
        second.Created.Should().BeNull();
        second.Error.Should().Be("One account per address.");
        asked
            .Select(x => (x.Name, x.Figure, x.Source, x.RemoteAddress))
            .Should()
            .Equal(
                (
                    "Erin",
                    PlayerAccountService.FEMALE_FIGURE,
                    RegistrationSource.Site,
                    "203.0.113.7"
                ),
                ("Fay", PlayerAccountService.FEMALE_FIGURE, RegistrationSource.Site, "203.0.113.7")
            );
        await using (var db = await _db.CreateDbContextAsync(Ct))
            (await db.Players.AnyAsync(x => x.Name == "Fay", Ct)).Should().BeFalse();

        await _events.WhenPublishedAsync<PlayerRegisteredEvent>(1);
        await _events.WhenPublishedAsync<PlayerRegistrationFailedEvent>(1);
        var registered = _events.Of<PlayerRegisteredEvent>().Single();
        var failed = _events.Of<PlayerRegistrationFailedEvent>().Single();

        (registered.Name, registered.Source, registered.RemoteAddress)
            .Should()
            .Be(("Erin", RegistrationSource.Site, "203.0.113.7"));
        (failed.Name, failed.Reason, failed.RemoteAddress)
            .Should()
            .Be(("Fay", "One account per address.", "203.0.113.7"));
        // The address is personal data: an event written to a log leaves it out.
        registered.ToString().Should().NotContain("203.0.113.7");
        failed.ToString().Should().NotContain("203.0.113.7").And.Contain("Fay");
    }

    public sealed class OnePerAddress(List<Turbo.Primitives.Policies.RegistrationRequest> asked)
        : Turbo.Primitives.Policies.IRegistrationPolicy
    {
        public ValueTask<Turbo.Primitives.Policies.RegistrationDecision> DecideAsync(
            Turbo.Primitives.Policies.RegistrationRequest request,
            CancellationToken ct
        )
        {
            var seen = asked.Any(x => x.RemoteAddress == request.RemoteAddress);

            asked.Add(request);

            return ValueTask.FromResult(
                seen
                    ? Turbo.Primitives.Policies.RegistrationDecision.Refuse(
                        "One account per address."
                    )
                    : Turbo.Primitives.Policies.RegistrationDecision.Allow
            );
        }
    }
}
