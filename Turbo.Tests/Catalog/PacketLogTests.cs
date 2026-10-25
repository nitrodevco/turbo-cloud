using FluentAssertions;
using Turbo.Admin.Catalog;
using Turbo.Primitives.Packets;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Reading a packet log: a packet's bytes are read exactly, by the name the log gives
/// it; its expression says the same bytes, and stands in for bytes that do not come to the
/// length they start with; a packet with neither readable says why. The log is a real one, of
/// Habbo's client being sent its gift wrapping, from a release whose header for it (377) is not
/// this hotel's.
/// </summary>
public sealed class PacketLogTests
{
    private const string NAME_LINE = "[GiftWrappingConfiguration]";

    private const string BYTES_LINE =
        "Incoming[377] -> [0][0][0]§[1]y[1][0][0][0][1][0][0][0][10][0][0][13],[0][0][13]-[0][0][13].[0][0][13]/[0][0][13]0[0][0][13]1[0][0][13]2[0][0][13]3[0][0][13]4[0][0][13]5[0][0][0][8][0][0][0][0][0][0][0][1][0][0][0][2][0][0][0][3][0][0][0][4][0][0][0][5][0][0][0][6][0][0][0][8][0][0][0][11][0][0][0][0][0][0][0][1][0][0][0][2][0][0][0][3][0][0][0][4][0][0][0][5][0][0][0][6][0][0][0][7][0][0][0][8][0][0][0][9][0][0][0][10][0][0][0][7][0][0][0]»[0][0][0]¼[0][0][0]½[0][0][0]¾[0][0][0]¿[0][0][0]À[0][0][0]Á";

    private const string EXPRESSION_LINE =
        "{in:GiftWrappingConfiguration}{b:true}{i:1}{i:10}{i:3372}{i:3373}{i:3374}{i:3375}{i:3376}{i:3377}{i:3378}{i:3379}{i:3380}{i:3381}{i:8}{i:0}{i:1}{i:2}{i:3}{i:4}{i:5}{i:6}{i:8}{i:11}{i:0}{i:1}{i:2}{i:3}{i:4}{i:5}{i:6}{i:7}{i:8}{i:9}{i:10}{i:7}{i:187}{i:188}{i:189}{i:190}{i:191}{i:192}{i:193}";

    private static string Log(params string[] lines) => string.Join("\r\n", lines);

    /// <summary>The gift wrapping as the client's <c>GiftWrappingConfigurationParser</c> reads it.</summary>
    private static string Wrapping(byte[] payload)
    {
        var packet = new ClientPacket(0, payload);
        var enabled = packet.PopBoolean();
        var price = packet.PopInt();
        var lists = new List<string>();

        for (var list = 0; list < 4; list++)
        {
            var count = packet.PopInt();
            lists.Add(string.Join(',', Enumerable.Range(0, count).Select(_ => packet.PopInt())));
        }

        return $"{enabled} {price} {string.Join(" | ", lists)}";
    }

    private const string WRAPPING =
        "True 1 3372,3373,3374,3375,3376,3377,3378,3379,3380,3381 | 0,1,2,3,4,5,6,8 | 0,1,2,3,4,5,6,7,8,9,10 | 187,188,189,190,191,192,193";

    [Fact]
    public void ThePacketsBytes_AreReadExactly_ByTheNameTheLogGivesIt()
    {
        var packet = PacketLog
            .Read(Log(NAME_LINE, BYTES_LINE, EXPRESSION_LINE))
            .Should()
            .ContainSingle()
            .Subject;

        packet.Name.Should().Be("GiftWrappingConfiguration");
        packet.Incoming.Should().BeTrue();
        packet.Header.Should().Be(377);
        packet.Line.Should().Be(2);
        packet.Problem.Should().BeNull();
        Wrapping(packet.Payload!).Should().Be(WRAPPING);
    }

    [Fact]
    public void TheExpression_SaysTheSameBytes()
    {
        var fromBytes = PacketLog.Read(Log(NAME_LINE, BYTES_LINE)).Single();
        var fromExpression = PacketLog.Read(EXPRESSION_LINE).Should().ContainSingle().Subject;

        fromExpression.Name.Should().Be("GiftWrappingConfiguration");
        fromExpression.Payload.Should().Equal(fromBytes.Payload);
    }

    [Fact]
    public void BytesThatAreCutShort_AreReadFromTheExpression()
    {
        var packet = PacketLog
            .Read(Log(NAME_LINE, BYTES_LINE[..^1], EXPRESSION_LINE))
            .Should()
            .ContainSingle()
            .Subject;

        packet.Problem.Should().BeNull();
        Wrapping(packet.Payload!).Should().Be(WRAPPING);
    }

    [Fact]
    public void APacketNeitherOfWhichCanBeRead_SaysWhy()
    {
        var packet = PacketLog
            .Read(Log(NAME_LINE, BYTES_LINE[..^1]))
            .Should()
            .ContainSingle()
            .Subject;

        packet.Payload.Should().BeNull();
        packet.Name.Should().Be("GiftWrappingConfiguration");
        packet.Problem.Should().Contain("not the 167 it starts with");
    }

    [Fact]
    public void ALogSavedInASingleByteCodePage_IsReadByte_ForByte()
    {
        var latin1 = System.Text.Encoding.Latin1.GetBytes(Log(NAME_LINE, BYTES_LINE));

        var packet = PacketLog.Read(PacketLog.TextOf(latin1)).Single();

        Wrapping(packet.Payload!).Should().Be(WRAPPING);
    }
}
