using FluentAssertions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The collectibles hub, which official Habbo shows every player (me menu "Collectibles",
/// <c>collectibles.hub.enabled</c> and <c>classic.collectibles.hub.enabled</c> true in the official
/// variables, 2026-10-12): it asks for the player's wallets on opening and each tab asks for its
/// list, and Turbo answered none of them, so the hub sat on its loading spinner. Turbo keeps no
/// wallets or NFTs, so every answer is the one Habbo gives an account with none (official client
/// capture, build WIN63-202610151745: no wallets, minting open, transfer fee 0), written as the AS3
/// parsers of that build read it (collectibles/*MessageEventParser.as).
/// </summary>
public sealed class CollectiblesHubTests
{
    private static async Task<(int Header, byte[] Body)> AskAsync(
        string message,
        Action<PayloadWriter> write
    )
    {
        var replies = await new PacketHarness().SendAsync(
            PacketHarness.Incoming(message),
            PacketHarness.Payload(write)
        );

        var reply = replies.Should().ContainSingle().Subject;

        return (reply.Header, reply.PopBytes(reply.Remaining));
    }

    [Fact]
    public async Task Opening_the_hub_is_told_the_player_has_no_wallets()
    {
        var (header, body) = await AskAsync("GetCollectibleWalletAddressesMessageEvent", _ => { });

        header.Should().Be(PacketHarness.Outgoing("CollectibleWalletAddressesMessageComposer"));
        // No Stardust wallet (""), then no other wallets.
        body.Should().Equal(Convert.FromHexString("000000000000"));
    }

    [Theory]
    [InlineData("GetNftCollectionsMessageEvent", "NftCollectionsMessageComposer")]
    [InlineData("GetNftClaimsMessageEvent", "NftClaimsMessageComposer")]
    public async Task A_wallet_list_the_hub_asks_for_comes_back_empty(string message, string answer)
    {
        var (header, body) = await AskAsync(message, w => w.String("0xwallet"));

        header.Should().Be(PacketHarness.Outgoing(answer));
        body.Should().Equal(Convert.FromHexString("00000000"));
    }

    [Theory]
    [InlineData("GetNftStoreOffersMessageEvent", "NftStoreOffersMessageComposer")]
    [InlineData(
        "GetCollectibleMintableItemTypesMessageEvent",
        "CollectableMintableItemTypesMessageComposer"
    )]
    [InlineData("GetMintTokenOffersMessageEvent", "CollectibleMintTokenOffersMessageComposer")]
    public async Task A_list_without_arguments_comes_back_empty(string message, string answer)
    {
        var (header, body) = await AskAsync(message, _ => { });

        header.Should().Be(PacketHarness.Outgoing(answer));
        body.Should().Equal(Convert.FromHexString("00000000"));
    }

    [Fact]
    public async Task A_wallets_collector_score_is_nothing_at_level_zero()
    {
        var (header, body) = await AskAsync(
            "GetCollectorScoreMessageEvent",
            w => w.String("0xwallet")
        );

        header.Should().Be(PacketHarness.Outgoing("NftCollectionsScoreMessageComposer"));
        body.Should().Equal(Convert.FromHexString("000000000000000000000000"));
    }

    [Fact]
    public async Task A_wallet_holds_no_mint_tokens()
    {
        var (header, body) = await AskAsync(
            "GetCollectibleMintTokensMessageEvent",
            w => w.String("0xwallet")
        );

        header.Should().Be(PacketHarness.Outgoing("CollectibleMintTokenCountMessageComposer"));
        body.Should().Equal(Convert.FromHexString("00000000"));
    }

    [Fact]
    public async Task Minting_is_answered_open_as_habbo_does()
    {
        var (header, body) = await AskAsync("GetCollectibleMintingEnabledMessageEvent", _ => { });

        header.Should().Be(PacketHarness.Outgoing("CollectibleMintingEnabledMessageComposer"));
        body.Should().Equal([1]);
    }

    [Fact]
    public async Task The_transfer_fee_is_zero_as_habbo_answers()
    {
        var (header, body) = await AskAsync("GetNftTransferFeeMessageEvent", _ => { });

        header.Should().Be(PacketHarness.Outgoing("NftTransferFeeMessageComposer"));
        body.Should().Equal(Convert.FromHexString("00000000"));
    }
}
