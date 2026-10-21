using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Grains.Navigator;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Navigator;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// A new Habbo account's navigator (recorded 2026-10-12 on hhus): every block but the
/// hotel view's hot rooms starts collapsed (NavigatorCollapsedCategories lists 21 fixed codes and
/// every room and event category by name), and the saved-search pane holds Public Rooms,
/// Recommended For You, My Rooms and My Favorite Rooms.
/// </summary>
public sealed class NavigatorNewPlayerDefaultsTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorNewPlayerDefaultsTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "navigator-defaults",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new NavigatorFlatCategoryEntity
            {
                Id = 14,
                Name = "Trading",
                Visible = true,
                Automatic = false,
                StaffOnly = false,
                MinRank = 1,
                OrderNum = 0,
            }
        );
        _db.Insert(
            new NavigatorEventCategoryEntity
            {
                Id = 1,
                Name = "Hottest Events",
                Visible = false,
            }
        );
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task a_new_player_starts_with_habbos_collapsed_blocks_and_saved_searches()
    {
        var snapshot = await (await ActivateAsync()).GetSnapshotAsync(Ct);

        snapshot
            .CollapsedSearchCodes.Should()
            .BeEquivalentTo([
                .. PlayerNavigatorConfig.HabboNewPlayerCollapsedSearchCodes,
                "category__Trading",
                "eventcategory__Hottest Events",
            ]);
        snapshot
            .SavedSearches.Select(x => (x.Id, x.SearchCode, x.Filter))
            .Should()
            .Equal(
                (1, "official", ""),
                (2, "recommended", ""),
                (3, "my", ""),
                (4, "favorites", "")
            );
    }

    [Fact]
    public async Task the_seeds_are_stored_once_and_choices_made_after_them_are_kept()
    {
        var first = await ActivateAsync();
        await first.DeleteSavedSearchAsync(1, Ct);
        await first.RemoveCollapsedSearchCodeAsync("recommended", Ct);
        await ((Grain)first).OnDeactivateAsync(new(DeactivationReasonCode.None, ""), Ct);

        var snapshot = await (await ActivateAsync()).GetSnapshotAsync(Ct);

        snapshot
            .SavedSearches.Select(x => x.SearchCode)
            .Should()
            .Equal("recommended", "my", "favorites");
        snapshot.CollapsedSearchCodes.Should().NotContain("recommended").And.Contain("hot");
    }

    [Fact]
    public async Task a_player_with_a_preference_of_their_own_is_not_seeded()
    {
        _db.Insert(
            new PlayerNavigatorCollapsedCategoryEntity
            {
                PlayerEntityId = PLAYER,
                SearchCode = "popular",
            }
        );

        var snapshot = await (await ActivateAsync()).GetSnapshotAsync(Ct);

        snapshot.CollapsedSearchCodes.Should().Equal("popular");
        snapshot.SavedSearches.Should().BeEmpty();
    }

    [Fact]
    public async Task a_hotel_can_turn_the_seeds_off_or_choose_its_own()
    {
        var off = await (
            await ActivateAsync(new() { SeedNewPlayerPreferences = false })
        ).GetSnapshotAsync(Ct);

        off.CollapsedSearchCodes.Should().BeEmpty();
        off.SavedSearches.Should().BeEmpty();

        var own = await (
            await ActivateAsync(
                new()
                {
                    NewPlayerCollapsedSearchCodes = ["popular"],
                    NewPlayerCollapsesCategories = false,
                    NewPlayerSavedSearches = ["my"],
                }
            )
        ).GetSnapshotAsync(Ct);

        own.CollapsedSearchCodes.Should().Equal("popular");
        own.SavedSearches.Select(x => x.SearchCode).Should().Equal("my");
    }

    private async Task<IPlayerNavigatorGrain> ActivateAsync(PlayerNavigatorConfig? config = null)
    {
        var grain = GrainHarness.Create<PlayerNavigatorGrain>(_fakes, _db, PLAYER);

        if (config is not null)
            GrainHarness.Replace<PlayerNavigatorConfig>(grain, config);

        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerNavigatorGrain)grain;
    }
}
