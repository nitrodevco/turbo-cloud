using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The rent confirmation's three requests, read in the order their Flash composers push them
/// (AS3 WIN63-202610131318): <c>GetRentOrBuyoutOfferMessageComposer(isWallItem, fullName, buyout)</c>,
/// <c>ExtendRentOrBuyoutFurniMessageComposer(isWallItem, objectId, buyout)</c> and
/// <c>ExtendRentOrBuyoutStripItemMessageComposer(stripItemId, buyout)</c>.
/// </summary>
public class RentOrBuyoutPacketTests
{
    [Fact]
    public void the_offer_request_names_the_furni_type_and_whether_it_is_a_buyout()
    {
        var message = Assert.IsType<GetRentOrBuyoutOfferMessage>(
            PacketHarness.Parse(
                PacketHarness.Incoming("GetRentOrBuyoutOfferMessageEvent"),
                PacketHarness.Payload(w => w.Bool(true).String("rare_dragonlamp*4").Bool(true))
            )
        );

        Assert.True(message.IsWallItem);
        Assert.Equal("rare_dragonlamp*4", message.FurniTypeName);
        Assert.True(message.Buyout);
    }

    [Fact]
    public void extending_names_the_room_furni_or_the_inventory_item()
    {
        var furni = Assert.IsType<ExtendRentOrBuyoutFurniMessage>(
            PacketHarness.Parse(
                PacketHarness.Incoming("ExtendRentOrBuyoutFurniMessageEvent"),
                PacketHarness.Payload(w => w.Bool(false).Int(812).Bool(true))
            )
        );
        Assert.False(furni.IsWallItem);
        Assert.Equal(812, furni.ObjectId);
        Assert.True(furni.Buyout);

        var strip = Assert.IsType<ExtendRentOrBuyoutStripItemMessage>(
            PacketHarness.Parse(
                PacketHarness.Incoming("ExtendRentOrBuyoutStripItemMessageEvent"),
                PacketHarness.Payload(w => w.Int(-77).Bool(false))
            )
        );
        Assert.Equal(-77, strip.StripItemId);
        Assert.False(strip.Buyout);
    }
}
