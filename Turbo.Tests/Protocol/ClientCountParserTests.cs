using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// A count the client sends is read with <c>PopList</c>, which bounds it by how many entries of
/// their smallest size the rest of the packet could hold: an overstated count reads what is
/// there. These parsers once looped on a raw <c>PopInt</c> count and threw at the packet's end.
/// </summary>
public sealed class ClientCountParserTests
{
    [Theory]
    [InlineData("CraftSecretMessageEvent")]
    [InlineData("GetCraftingRecipesAvailableMessageEvent")]
    public void A_mixer_list_whose_count_overstates_it_reads_the_ids_sent(string message)
    {
        var parsed = PacketHarness.Parse(
            PacketHarness.Incoming(message),
            PacketHarness.Payload(w => w.Int(7).Int(int.MaxValue).Int(11).Int(12))
        );

        var ids = parsed switch
        {
            CraftSecretMessage craft => craft.ItemIds,
            GetCraftingRecipesAvailableMessage recipes => recipes.ItemIds,
            _ => throw new InvalidOperationException(parsed.GetType().Name),
        };

        ids.Should().Equal(RoomObjectId.Parse(11), RoomObjectId.Parse(12));
    }

    [Fact]
    public void A_poll_answer_whose_count_overstates_it_reads_the_answers_sent()
    {
        var parsed = (PollAnswerMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("PollAnswerMessageEvent"),
                PacketHarness.Payload(w => w.Int(3).Int(4).Int(int.MaxValue).String("2"))
            );

        parsed.Answers.Should().Equal("2");
    }

    [Fact]
    public void A_marketplace_offer_whose_count_overstates_it_reads_the_items_sent()
    {
        var parsed = (MakeOfferMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("MakeOfferMessageEvent"),
                PacketHarness.Payload(w => w.Int(50).Int(1).Int(int.MaxValue).Int(21).Int(22))
            );

        parsed.ItemRefs.Should().Equal(21, 22);
    }
}
