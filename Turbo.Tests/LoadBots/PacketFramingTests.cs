using Turbo.LoadBots.Protocol;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.LoadBots;

/// <summary>
/// The load bots' framing: a length, a header and a body, taken off a receive buffer one whole
/// message at a time.
/// </summary>
public class PacketFramingTests
{
    [Fact]
    public void Frame_IsReadBackAsTheSameHeaderAndBody()
    {
        byte[] body = [1, 2, 3, 4, 5];
        var frame = PacketFraming.Frame(1234, body);

        Assert.True(PacketFraming.TryReadFrame(frame, out var message, out var consumed));

        Assert.Equal(frame.Length, consumed);
        Assert.NotNull(message);
        Assert.Equal(1234, message.Header);
        Assert.Equal(body, message.Body.ToArray());
    }

    [Fact]
    public void Frame_MatchesTheBytesTheServerSerializerWrites()
    {
        var composer = new HeightMapMessageComposer
        {
            Width = 2,
            Size = 2,
            Heights = [0, 256],
        };
        var serializer = PacketHarness.Revision.Serializers[composer.GetType()];
        using var packet = serializer.Serialize(composer);
        var serverBytes = packet.ToArray();

        var encoded = PacketHarness.Encode(composer);
        var body = encoded.PopBytes(encoded.Remaining);

        Assert.Equal(serverBytes, PacketFraming.Frame(serializer.Header, body));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    public void TryReadFrame_OnAPartialBuffer_ReturnsFalseAndConsumesNothing(int length)
    {
        var frame = PacketFraming.Frame(1234, [1, 2, 3, 4, 5]);

        Assert.False(
            PacketFraming.TryReadFrame(frame.AsSpan(0, length), out var message, out var consumed)
        );

        Assert.Null(message);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void TryReadFrame_ReadsTwoBackToBackFramesOneAtATime()
    {
        var first = PacketFraming.Frame(10, [1, 2]);
        var second = PacketFraming.Frame(20, [3, 4, 5]);
        byte[] buffer = [.. first, .. second];

        Assert.True(PacketFraming.TryReadFrame(buffer, out var one, out var consumedOne));
        Assert.Equal(first.Length, consumedOne);
        Assert.Equal(10, one!.Header);
        Assert.Equal([1, 2], one.Body.ToArray());

        Assert.True(
            PacketFraming.TryReadFrame(buffer.AsSpan(consumedOne), out var two, out var consumedTwo)
        );
        Assert.Equal(second.Length, consumedTwo);
        Assert.Equal(20, two!.Header);
        Assert.Equal([3, 4, 5], two.Body.ToArray());

        Assert.False(
            PacketFraming.TryReadFrame(
                buffer.AsSpan(consumedOne + consumedTwo),
                out _,
                out var consumedNone
            )
        );
        Assert.Equal(0, consumedNone);
    }
}
