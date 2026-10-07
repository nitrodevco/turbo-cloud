using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room.Engine;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The Flash client sends a pickup's category as <c>PickupObjectMessageComposer</c> maps it: a
/// floor item as 2 and a wall item as 1 (room object categories 10 and 20). Anything else - the room
/// object categories themselves included - picks up nothing.
/// </summary>
public sealed class PickupObjectCategoryTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(1, true)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    [InlineData(100, false)]
    public async Task A_pickup_is_made_only_for_the_categories_flash_sends(
        int category,
        bool picksUp
    )
    {
        var fakes = new Fakes();
        var handler = new PickupObjectMessageHandler(fakes.Create<IRoomService>());

        await handler.HandleAsync(
            new PickupObjectMessage
            {
                CategoryId = category,
                ObjectId = 55,
                Confirm = false,
            },
            new MessageContext(fakes.Create<ISessionContext>(), 10, -1),
            TestContext.Current.CancellationToken
        );

        fakes
            .Log.On<IRoomService>()
            .Count(x => x.Method == "PickupItemInRoomAsync")
            .Should()
            .Be(picksUp ? 1 : 0);
    }
}
