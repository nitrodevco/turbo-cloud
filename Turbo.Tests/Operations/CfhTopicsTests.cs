using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Moderation;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The call for help topics a client is sent at login. <c>CfhTopicsInitMessageParser</c> reads a
/// count of categories, each its name and a count of topics, each topic its name, id and
/// consequence. Categories come in the order of their first topic.
/// </summary>
public sealed class CfhTopicsTests : IDisposable
{
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Topics_are_grouped_by_category_in_their_order_and_a_disabled_one_is_left_out()
    {
        _db.Insert(Topic(12, "trolling_bad_behavior", "bullying", "mods_till_logout", 400));
        _db.Insert(Topic(1, "sexual_content", "explicit_sexual_talk", "mods", 100));
        _db.Insert(
            Topic(34, "trolling_bad_behavior", "inappropiate_room_group_event", "mods", 407)
        );
        _db.Insert(Topic(2, "sexual_content", "cybersex", "mods", 101, enabled: false));

        var categories = await new CallForHelpService(
            _db,
            Options.Create(new OperationsConfig()),
            new Fakes().Create<IHotelTextProvider>(),
            TimeProvider.System
        ).GetTopicsAsync(Ct);

        categories.Select(x => x.Name).Should().Equal("sexual_content", "trolling_bad_behavior");
        categories[0].Topics.Select(x => x.Id).Should().Equal(1);
        categories[1]
            .Topics.Select(x => (x.Id, x.Name))
            .Should()
            .Equal((12, "bullying"), (34, "inappropiate_room_group_event"));
    }

    [Fact]
    public void The_topics_are_written_as_the_client_reads_them()
    {
        var reply = PacketHarness.Encode(
            new CfhTopicsInitMessageComposer
            {
                Categories =
                [
                    new()
                    {
                        Name = "game_interruption",
                        Topics =
                        [
                            new()
                            {
                                Id = 22,
                                Name = "flooding",
                                Consequence = "mods_till_logout",
                            },
                            new()
                            {
                                Id = 23,
                                Name = "door_blocking",
                                Consequence = "auto_reply",
                            },
                        ],
                    },
                ],
            }
        );

        Assert.Equal(1, reply.PopInt());
        Assert.Equal("game_interruption", reply.PopString());
        Assert.Equal(2, reply.PopInt());
        Assert.Equal("flooding", reply.PopString());
        Assert.Equal(22, reply.PopInt());
        Assert.Equal("mods_till_logout", reply.PopString());
        Assert.Equal("door_blocking", reply.PopString());
        Assert.Equal(23, reply.PopInt());
        Assert.Equal("auto_reply", reply.PopString());
        Assert.True(reply.End);
    }

    private static CfhTopicEntity Topic(
        int id,
        string category,
        string name,
        string consequence,
        int sortOrder,
        bool enabled = true
    ) =>
        new()
        {
            Id = id,
            Category = category,
            Name = name,
            Consequence = consequence,
            SortOrder = sortOrder,
            Enabled = enabled,
        };
}
