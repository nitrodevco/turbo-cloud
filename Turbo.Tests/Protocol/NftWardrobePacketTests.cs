using System.Threading.Tasks;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Nft;
using Turbo.Primitives.Messages.Outgoing.Nft;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The avatar editor's NFT wardrobe in the AS3's field order: the outfits list
/// (<c>NftWardrobeItem</c>: id, figure, gender, token id, contract key), the selection (current
/// token id, fallback figure, fallback gender) and the save (the outfit id). The hotel has no NFT
/// outfits, so both requests are answered empty.
/// </summary>
public class NftWardrobePacketTests
{
    [Fact]
    public async Task The_wardrobe_request_is_answered_with_an_empty_list()
    {
        var sent = await new PacketHarness().SendAsync(
            PacketHarness.Incoming("GetUserNftWardrobeMessageEvent"),
            []
        );

        sent.Should().ContainSingle();
        sent[0].Header.Should().Be(PacketHarness.Outgoing("UserNftWardrobeMessageComposer"));
        sent[0].PopInt().Should().Be(0);
        sent[0].End.Should().BeTrue();
    }

    [Fact]
    public async Task The_selection_request_is_answered_with_no_outfit_worn()
    {
        var sent = await new PacketHarness().SendAsync(
            PacketHarness.Incoming("GetSelectedNftWardrobeOutfitMessageEvent"),
            []
        );

        sent.Should().ContainSingle();
        sent[0]
            .Header.Should()
            .Be(PacketHarness.Outgoing("UserNftWardrobeSelectionMessageComposer"));
        sent[0].PopString().Should().BeEmpty();
        sent[0].PopString().Should().BeEmpty();
        sent[0].PopString().Should().BeEmpty();
        sent[0].End.Should().BeTrue();
    }

    [Fact]
    public void An_outfit_is_written_as_NftWardrobeItem_reads_it()
    {
        var packet = PacketHarness.Encode(
            new UserNftWardrobeMessageComposer
            {
                Items =
                [
                    new()
                    {
                        Id = "o1",
                        Figure = "hd-180-1",
                        Gender = "M",
                        TokenId = "42",
                        ContractKey = "c",
                    },
                ],
            }
        );

        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("o1");
        packet.PopString().Should().Be("hd-180-1");
        packet.PopString().Should().Be("M");
        packet.PopString().Should().Be("42");
        packet.PopString().Should().Be("c");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void The_save_reads_the_outfit_id()
    {
        var message = PacketHarness.Parse(
            PacketHarness.Incoming("SaveUserNftWardrobeMessageEvent"),
            PacketHarness.Payload(w => w.String("o1"))
        );

        message.Should().BeEquivalentTo(new SaveUserNftWardrobeMessage { OutfitId = "o1" });
    }
}
