using FluentAssertions;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Inventory.Purse;
using Turbo.Players.Grains;
using Turbo.Primitives.Messages.Incoming.Inventory;
using Turbo.Primitives.Messages.Outgoing.Inventory.Purse;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Habbo (hhus, 2026-10-12): ActivityPoints goes out right after AuthenticationOK and lists every
/// activity point type, a new account's at 0; GetCreditsInfo is answered with CreditBalance only.
/// </summary>
public sealed class WalletActivityPointsTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task every_activity_point_type_is_listed_even_at_zero()
    {
        _fakes.Handlers[nameof(ICurrencyTypeProvider.GetEnabledCurrencyNames)] = _ =>
            (IReadOnlyCollection<string>)["credits", "duckets", "diamonds"];
        _fakes.Handlers[nameof(ICurrencyTypeProvider.TryGetCurrencyKindByName)] = call =>
        {
            call.Args[1] = (string)call.Args[0]! switch
            {
                "duckets" => CurrencyKind.ActivityPoints(0),
                "diamonds" => CurrencyKind.ActivityPoints(5),
                _ => CurrencyKind.Credits,
            };
            return true;
        };
        var grain = GrainHarness.Create<PlayerWalletGrain>(_fakes, new InMemoryDb());
        GrainHarness.Replace<ICurrencyTypeProvider>(grain, _fakes.Create<ICurrencyTypeProvider>());
        GrainHarness.Replace<IPlayerNoticeService>(grain, _fakes.Create<IPlayerNoticeService>());
        await ((Grain)grain).OnActivateAsync(Ct);

        var points = await ((IPlayerWalletGrain)grain).GetActivityPointsAsync(Ct);

        points.Should().BeEquivalentTo(new Dictionary<int, int> { [0] = 0, [5] = 0 });
    }

    [Fact]
    public async Task credits_info_is_answered_with_the_credits_only()
    {
        await new GetCreditsInfoMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new GetCreditsInfoMessage(),
            new MessageContext(_fakes.Create<ISessionContext>(), 1, -1),
            Ct
        );

        _fakes
            .Sent()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<CreditBalanceEventMessageComposer>();
    }
}
