using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Catalog;
using Turbo.PacketHandlers.Users;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Two requests the client sends at login that Habbo answers (recorded on hhus,
/// 2026-10-12) and Turbo left unanswered: BlockListInit gets the blocked users (BlockList, empty
/// for a new account), GetNextTargetedOffer gets an empty TargetedOfferNotFound.
/// </summary>
public sealed class UnansweredLoginRequestsTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task the_block_list_is_sent_so_blocks_survive_a_new_session()
    {
        _fakes.Handlers["GetBlockedAsync"] = _ =>
            Task.FromResult(new List<PlayerId> { (PlayerId)5, (PlayerId)7 });

        await new BlockListInitMessageHandler(_fakes.Create<IGrainFactory>()).HandleAsync(
            new BlockListInitMessage(),
            Context(),
            Ct
        );

        var composer = Assert.IsType<BlockListMessageComposer>(
            _fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        Assert.Equal([5, 7], composer.BlockedUserIds);
    }

    [Fact]
    public async Task no_targeted_offer_is_answered_as_not_found()
    {
        await new GetNextTargetedOfferMessageHandler().HandleAsync(
            new GetNextTargetedOfferMessage(),
            Context(),
            Ct
        );

        Assert.IsType<TargetedOfferNotFoundEventMessageComposer>(
            _fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
    }

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, -1);
}
