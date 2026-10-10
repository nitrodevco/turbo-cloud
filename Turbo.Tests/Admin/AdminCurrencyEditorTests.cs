using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Primitives.Players.Providers;
using Turbo.Tests.Catalog;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Admin;

/// <summary>
/// Currency types as the panel manages them: listed with what uses each, added and changed with
/// the server's mapping reloaded each time, and refused, with why, when a name or kind is taken, a
/// kind would change under balances, prices or vouchers, or credits would be turned off or deleted.
/// </summary>
public sealed class AdminCurrencyEditorTests : IDisposable
{
    private const string ACTOR = "panel:tester";

    private readonly CatalogFixture _catalog = new();
    private readonly AdminCurrencyEditor _editor;

    public AdminCurrencyEditorTests()
    {
        _editor = new AdminCurrencyEditor(
            _catalog.Db,
            _catalog.Fakes.Create<ICurrencyTypeProvider>(),
            NullLogger<AdminCurrencyEditor>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private int Reloads() =>
        _catalog.Fakes.Log.Calls.Count(x => x.Method == nameof(ICurrencyTypeProvider.ReloadAsync));

    private async Task<CurrencyItem> ItemAsync(int id) =>
        (await _editor.ListAsync(Ct)).Items.Single(x => x.Id == id);

    [Fact]
    public async Task The_list_says_what_each_currency_is_and_what_uses_it()
    {
        var items = (await _editor.ListAsync(Ct)).Items;

        items
            .Select(x => (x.Name, x.Type, x.ActivityPointType))
            .Should()
            .Equal(
                ("credits", "credits", null),
                ("duckets", "activity_points", 0),
                ("diamonds", "activity_points", 5)
            );
        items.Single(x => x.Id == DUCKETS_ROW).Offers.Should().Be(1);
    }

    [Fact]
    public async Task A_new_currency_is_added_and_the_server_reloads_it()
    {
        var id = await _editor.SaveAsync(
            0,
            new CurrencyRequest("Seasonal", "activity_points", 105, null),
            ACTOR,
            Ct
        );

        var item = await ItemAsync(id);

        item.Should()
            .Match<CurrencyItem>(x =>
                x.Name == "seasonal"
                && x.Type == "activity_points"
                && x.ActivityPointType == 105
                && x.Enabled
            );
        Reloads().Should().Be(1);
    }

    [Theory]
    [InlineData("bad name", "activity_points", 7, "lowercase letters")]
    [InlineData("duckets", "activity_points", 7, "called duckets already")]
    [InlineData("pixels", "activity_points", 0, "Activity points 0 are a currency already")]
    [InlineData("pixels", "activity_points", null, "the number the client shows them by")]
    [InlineData("pixels", "gold", null, "credits, silver, emeralds or activity points")]
    [InlineData("more_credits", "credits", null, "credits is a currency already")]
    public async Task A_currency_it_cannot_take_is_refused_with_why(
        string name,
        string type,
        int? activityPointType,
        string why
    )
    {
        var save = () =>
            _editor.SaveAsync(
                0,
                new CurrencyRequest(name, type, activityPointType, null),
                ACTOR,
                Ct
            );

        (await save.Should().ThrowAsync<ArgumentException>()).WithMessage($"*{why}*");
        (await _editor.ListAsync(Ct)).Items.Should().HaveCount(3);
        Reloads().Should().Be(0);
    }

    [Fact]
    public async Task A_currency_in_use_is_renamed_and_turned_off_but_keeps_its_kind()
    {
        await _editor.SaveAsync(
            DUCKETS_ROW,
            new CurrencyRequest("pixels", "activity_points", 0, false),
            ACTOR,
            Ct
        );

        (await ItemAsync(DUCKETS_ROW))
            .Should()
            .Match<CurrencyItem>(x => x.Name == "pixels" && !x.Enabled);

        var rekind = () =>
            _editor.SaveAsync(
                DUCKETS_ROW,
                new CurrencyRequest("pixels", "activity_points", 9, null),
                ACTOR,
                Ct
            );

        (await rekind.Should().ThrowAsync<ArgumentException>()).WithMessage(
            "*1 catalog offer is priced in it*Add a new currency instead*"
        );
        (await ItemAsync(DUCKETS_ROW)).ActivityPointType.Should().Be(0);
    }

    [Fact]
    public async Task Credits_are_never_turned_off_or_deleted()
    {
        var off = () =>
            _editor.SaveAsync(
                CREDITS_ROW,
                new CurrencyRequest("credits", "credits", null, false),
                ACTOR,
                Ct
            );
        var delete = () => _editor.DeleteAsync(CREDITS_ROW, ACTOR, Ct);

        (await off.Should().ThrowAsync<ArgumentException>()).WithMessage("*can't be turned off*");
        (await delete.Should().ThrowAsync<ArgumentException>()).WithMessage("*can't be deleted*");
        (await ItemAsync(CREDITS_ROW)).Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Only_a_currency_nothing_uses_is_deleted()
    {
        var unused = await _editor.SaveAsync(
            0,
            new CurrencyRequest("seasonal", "activity_points", 105, null),
            ACTOR,
            Ct
        );
        _catalog.AddOffer(200, FURNITURE, currency: 3, currencyRow: unused);

        var priced = () => _editor.DeleteAsync(unused, ACTOR, Ct);

        (await priced.Should().ThrowAsync<ArgumentException>()).WithMessage(
            "*1 catalog offer is priced in it; turn it off instead*"
        );

        var other = await _editor.SaveAsync(
            0,
            new CurrencyRequest("tokens", "silver", null, null),
            ACTOR,
            Ct
        );

        (await _editor.DeleteAsync(other, ACTOR, Ct)).Should().BeTrue();
        (await _editor.ListAsync(Ct)).Items.Select(x => x.Id).Should().NotContain(other);
        (await _editor.DeleteAsync(other, ACTOR, Ct)).Should().BeFalse();
    }
}
