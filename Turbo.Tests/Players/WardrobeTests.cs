using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Wardrobe;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A look saved to the wardrobe, read back at the next login. A saved outfit allowed 100
/// characters while a player may wear 279, so a full look (every set type, two colours) could be
/// worn and not saved.
/// </summary>
public sealed class WardrobeTests : IDisposable
{
    private const int PLAYER = 1;

    // Every set type a figure has, each with two colours: 191 characters.
    private const string FULL_LOOK =
        "hr-3163-61-1408.hd-180-1-1408.ch-3030-82-1408.lg-275-1408-1408.sh-3089-110-1408."
        + "ha-1002-1408-1408.he-1608-1408-1408.ea-1404-63-1408.fa-1201-1408-1408."
        + "ca-1804-64-1408.wa-2001-1408-1408.cc-3039-1408-1408";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WardrobeTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
            }
        );

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_look_as_long_as_a_player_may_wear_is_saved_and_there_at_the_next_login()
    {
        FULL_LOOK
            .Length.Should()
            .BeGreaterThan(100)
            .And.BeLessThanOrEqualTo(PlayerEntity.FIGURE_MAX_LENGTH);

        (await (await WardrobeAsync()).SaveOutfitAsync(1, FULL_LOOK, AvatarGenderType.Male, Ct))
            .Should()
            .BeTrue();

        var outfits = await (await WardrobeAsync()).GetOutfitsAsync(Ct);

        outfits.Should().ContainSingle().Which.Figure.Should().Be(FULL_LOOK);
    }

    private async Task<IPlayerWardrobeGrain> WardrobeAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Wardrobe.PlayerWardrobeGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerWardrobeGrain)grain;
    }
}
