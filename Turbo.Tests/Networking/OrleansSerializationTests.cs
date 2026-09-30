using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Networking;
using Xunit;

namespace Turbo.Tests.Networking;

public class OrleansSerializationTests
{
    [Fact]
    public void MessengerComposer_RoundTripsThroughItsInterface()
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<Serializer>();
        IComposer original = new NewConsoleMessageMessageComposer
        {
            ChatId = 42,
            Message = "Serializer roundtrip",
            SecondsSinceSent = 3,
            MessageId = "message-1",
            ConfirmationId = 9,
            SenderId = 7,
            SenderName = "Sender",
            SenderFigure = "hr-100-61",
        };

        // Packet harnesses exercise Habbo's wire format, not the generated codecs
        // Orleans needs when the same composer crosses grain and stream boundaries.
        var bytes = serializer.SerializeToArray(original);
        var decoded = serializer.Deserialize<IComposer>(bytes);

        Assert.Equal(original, Assert.IsType<NewConsoleMessageMessageComposer>(decoded));
    }
}
