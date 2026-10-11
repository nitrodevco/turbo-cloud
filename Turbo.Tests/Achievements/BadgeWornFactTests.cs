using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Inventory;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

/// <summary>
/// Putting a badge on is a fact a WEAR_BADGE quest counts. A badge the player has just been
/// given is worn by nobody, so putting it on for the first time counts too.
/// </summary>
public sealed class BadgeWornFactTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BadgeWornFactTests()
    {
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

        _fakes.Handlers[nameof(IBadgeDirectoryGrain.GetInfoAsync)] = call =>
            Task.FromResult(
                ((ImmutableArray<string>)call.Args[0]!)
                    .Select(code => new BadgeInfoSnapshot
                    {
                        BadgeCode = code,
                        OwnerCount = 1,
                        Rarity = BadgeRarityType.Common,
                    })
                    .ToImmutableArray()
            );
        _fakes.Handlers[nameof(IBadgeDirectoryGrain.OnBadgeGrantedAsync)] = call =>
            Task.FromResult(
                new BadgeInfoSnapshot
                {
                    BadgeCode = (string)call.Args[0]!,
                    OwnerCount = 1,
                    Rarity = BadgeRarityType.Common,
                }
            );
        _fakes.Handlers[nameof(IBadgeDirectoryGrain.GetRequestableBadgeAsync)] = _ => null;
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// The new badge's row was inserted with no slot, and the slot column's default of 0 was
    /// stored in its place: read back, the badge looked taken off rather than never worn, and
    /// putting it on recorded nothing.
    /// </summary>
    [Fact]
    public async Task Putting_on_a_badge_just_given_counts_once_it_has_been_saved_and_read_back()
    {
        await (await BadgesAsync()).GiveBadgeAsync("W2601", Ct);

        await (await BadgesAsync()).SetActivatedBadgesAsync(["W2601"], Ct);

        _fakes
            .Log.Of(nameof(IAchievementFactRecorder.Record))
            .Select(call => (AchievementFact)call.Args[2]!)
            .Should()
            .ContainSingle(fact => fact.Source == AchievementSources.BADGE_WORN)
            .Which.Value.Should()
            .Be("W2601");
    }

    private async Task<IPlayerBadgeGrain> BadgesAsync()
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.Badges.PlayerBadgeGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerBadgeGrain)grain;
    }
}
