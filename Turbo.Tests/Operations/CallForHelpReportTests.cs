using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// A call for help from the client's bytes to its row and back. <c>CallForHelpMessageComposer</c>
/// sends the message, topic, reported user, room, a count of chat lines (user id, text), then a
/// name and an email. The reply is <c>CallForHelpResult</c> (a type the client ignores, then a
/// text it shows, its own <c>help.cfh.sent.text</c> when empty). The reporter's report status
/// lists the report, and a report closed without action can be appealed once.
/// </summary>
public sealed class CallForHelpReportTests : IDisposable
{
    private const int REPORTER = 1;
    private const int REPORTED = 2;
    private const int ROOM = 9;
    private const int TOPIC = 12;
    private const int GROUP = 30;
    private const int THREAD = 40;

    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CallForHelpReportTests()
    {
        _db.Insert(Player(REPORTER, "reporter"));
        _db.Insert(Player(REPORTED, "bully"));
        _db.Insert(
            new CfhTopicEntity
            {
                Id = TOPIC,
                Category = "trolling_bad_behavior",
                Name = "bullying",
                Consequence = "mods_till_logout",
            }
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_report_is_stored_with_its_chat_lines_and_answered_as_sent()
    {
        var reply = Assert.Single(await SendReportAsync(TOPIC));

        Assert.Equal(PacketHarness.Outgoing("CallForHelpResultMessageComposer"), reply.Header);
        Assert.Equal((int)CfhResultType.Sent, reply.PopInt());
        Assert.Equal(string.Empty, reply.PopString());
        Assert.True(reply.End);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var report = await db.CfhReports.Include(x => x.ChatLines).SingleAsync(Ct);

        (report.ReporterEntityId, report.ReportedEntityId, report.RoomEntityId, report.TopicId)
            .Should()
            .Be((REPORTER, REPORTED, ROOM, TOPIC));
        report.Message.Should().Be("they keep insulting me");
        report.ReporterName.Should().BeNull("an empty name is none");
        report.ClosedAt.Should().BeNull();
        report
            .ChatLines!.OrderBy(x => x.Position)
            .Select(x => (x.PlayerEntityId, x.Text))
            .Should()
            .Equal((REPORTED, "you are stupid"), (REPORTER, "leave me alone"));
    }

    [Fact]
    public async Task The_reporter_sees_their_report_waiting()
    {
        await SendReportAsync(TOPIC);

        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("GetCfhMyReportStatusMessageEvent"),
                    [],
                    playerId: REPORTER
                )
        );

        Assert.Equal(1, reply.PopInt());
        Assert.True(reply.PopLong() > 0);
        Assert.True(reply.PopLong() > 0);
        Assert.Equal("they keep insulting me", reply.PopString());
        Assert.Equal(TOPIC, reply.PopInt());
        Assert.Equal("bully", reply.PopString());
        Assert.Equal(-1, reply.PopLong());
        Assert.False(reply.PopBoolean());
        Assert.False(reply.PopBoolean());
        Assert.Equal((byte)CfhAppealStatusType.None, reply.PopByte());
        Assert.Equal(-1, reply.PopLong());
        Assert.Equal(-1, reply.PopLong());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task A_topic_the_hotel_does_not_have_is_refused_and_nothing_is_stored()
    {
        var reply = Assert.Single(await SendReportAsync(topic: 999));

        Assert.Equal((int)CfhResultType.Refused, reply.PopInt());
        Assert.Equal("Choose what your report is about, then send it again.", reply.PopString());
        (await Reports()).Should().BeEmpty();
    }

    [Fact]
    public async Task One_more_than_the_open_reports_allowed_is_refused()
    {
        for (var i = 0; i < new OperationsConfig().CfhMaxOpenReports; i++)
            await SendReportAsync(TOPIC);

        var reply = Assert.Single(await SendReportAsync(TOPIC));

        Assert.Equal((int)CfhResultType.Refused, reply.PopInt());
        Assert.Contains("3 reports waiting", reply.PopString());
        (await Reports()).Should().HaveCount(3);
    }

    [Fact]
    public async Task Only_a_report_closed_without_action_can_be_appealed_and_only_once()
    {
        await SendReportAsync(TOPIC);
        var id = (await Reports()).Single().Id;
        var service = Service();

        (await service.AppealAsync(REPORTER, id, Ct)).Should().BeFalse("it is still open");

        await Close(id, sanctioned: false);

        (await service.AppealAsync(REPORTED, id, Ct)).Should().BeFalse("it is not theirs");

        var replies = await Harness()
            .SendAsync(
                PacketHarness.Incoming("AppealCfhMessageEvent"),
                PacketHarness.Payload(w => w.Int(id)),
                playerId: REPORTER
            );

        Assert.Equal(
            PacketHarness.Outgoing("MyCfhReportStatusMessageComposer"),
            Assert.Single(replies).Header
        );
        var report = (await Reports()).Single();
        report.AppealStatus.Should().Be(CfhAppealStatusType.Appealed);
        report.AppealCreatedAt.Should().Be(NOW);
        (await service.AppealAsync(REPORTER, id, Ct)).Should().BeFalse("it is appealed already");
    }

    [Fact]
    public async Task A_report_that_led_to_a_sanction_cannot_be_appealed()
    {
        await SendReportAsync(TOPIC);
        var id = (await Reports()).Single().Id;

        await Close(id, sanctioned: true);

        (await Service().AppealAsync(REPORTER, id, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_report_from_the_messenger_is_stored_with_the_conversation_and_no_room()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("CallForHelpFromIMMessageEvent"),
                    PacketHarness.Payload(w =>
                        w.String("in my messages")
                            .Int(TOPIC)
                            .Int(REPORTED)
                            .Int(1)
                            .Int(REPORTED)
                            .String("send me your password")
                            .String(string.Empty)
                            .String(string.Empty)
                    ),
                    playerId: REPORTER
                )
        );

        Assert.Equal((int)CfhResultType.Sent, reply.PopInt());

        await using var db = await _db.CreateDbContextAsync(Ct);
        var report = await db.CfhReports.Include(x => x.ChatLines).SingleAsync(Ct);

        report.Source.Should().Be(CfhSourceType.InstantMessage);
        (report.ReportedEntityId, report.RoomEntityId, report.Message)
            .Should()
            .Be((REPORTED, null, "in my messages"));
        report
            .ChatLines!.Select(x => (x.PlayerEntityId, x.Text))
            .Should()
            .Equal((REPORTED, "send me your password"));
    }

    [Fact]
    public async Task A_report_of_a_photo_is_stored_with_the_photo()
    {
        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("CallForHelpFromPhotoMessageEvent"),
                    PacketHarness.Payload(w =>
                        w.String("photo-abc123")
                            .Int(ROOM)
                            .Int(REPORTED)
                            .Int(TOPIC)
                            .Int(55)
                            .String(string.Empty)
                            .String(string.Empty)
                    ),
                    playerId: REPORTER
                )
        );

        Assert.Equal((int)CfhResultType.Sent, reply.PopInt());

        var report = (await Reports()).Single();

        report.Source.Should().Be(CfhSourceType.Photo);
        (report.ExtraDataId, report.ItemEntityId, report.RoomEntityId, report.ReportedEntityId)
            .Should()
            .Be(("photo-abc123", 55, ROOM, REPORTED));
        report.Message.Should().BeEmpty();
    }

    [Fact]
    public async Task A_report_of_a_forum_thread_reports_its_author()
    {
        InsertForumPost();

        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("CallForHelpFromForumThreadMessageEvent"),
                    PacketHarness.Payload(w =>
                        w.Int(GROUP)
                            .Int(THREAD)
                            .Int(TOPIC)
                            .String("a rude thread")
                            .String(string.Empty)
                            .String(string.Empty)
                    ),
                    playerId: REPORTER
                )
        );

        Assert.Equal((int)CfhResultType.Sent, reply.PopInt());
        var report = (await Reports()).Single();
        report.Source.Should().Be(CfhSourceType.ForumThread);
        (report.GuildEntityId, report.ForumThreadEntityId, report.ForumMessageId)
            .Should()
            .Be((GROUP, THREAD, (int?)null));
        (report.ReportedEntityId, report.TopicId, report.Message)
            .Should()
            .Be((REPORTED, TOPIC, "a rude thread"));
    }

    [Fact]
    public async Task A_report_of_a_forum_message_reports_its_author()
    {
        InsertForumPost();

        var reply = Assert.Single(
            await Harness()
                .SendAsync(
                    PacketHarness.Incoming("CallForHelpFromForumMessageMessageEvent"),
                    PacketHarness.Payload(w =>
                        w.Int(GROUP)
                            .Int(THREAD)
                            .Int(1)
                            .Int(TOPIC)
                            .String("a rude reply")
                            .String(string.Empty)
                            .String(string.Empty)
                    ),
                    playerId: REPORTER
                )
        );

        Assert.Equal((int)CfhResultType.Sent, reply.PopInt());
        var report = (await Reports()).Single();
        report.Source.Should().Be(CfhSourceType.ForumMessage);
        (report.GuildEntityId, report.ForumThreadEntityId, report.ForumMessageId)
            .Should()
            .Be((GROUP, THREAD, 1));
        (report.ReportedEntityId, report.Message).Should().Be((REPORTED, "a rude reply"));
    }

    private void InsertForumPost()
    {
        _db.Insert(
            new GuildForumThreadEntity
            {
                Id = THREAD,
                GuildEntityId = GROUP,
                PlayerEntityId = REPORTED,
                Subject = "A rude subject",
            }
        );
        _db.Insert(
            new GuildForumMessageEntity
            {
                Id = 1,
                GuildEntityId = GROUP,
                ThreadEntityId = THREAD,
                ForumMessageId = 1,
                ThreadIndex = 0,
                PlayerEntityId = REPORTED,
                Text = "you are stupid",
            }
        );
    }

    private async Task<List<ClientPacket>> SendReportAsync(int topic) =>
        await Harness()
            .SendAsync(
                PacketHarness.Incoming("CallForHelpMessageEvent"),
                PacketHarness.Payload(w =>
                    w.String("they keep insulting me")
                        .Int(topic)
                        .Int(REPORTED)
                        .Int(ROOM)
                        .Int(2)
                        .Int(REPORTED)
                        .String("you are stupid")
                        .Int(REPORTER)
                        .String("leave me alone")
                        .String(string.Empty)
                        .String(string.Empty)
                ),
                playerId: REPORTER
            );

    private PacketHarness Harness()
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(ICallForHelpService)] = Service();
        return harness;
    }

    private CallForHelpService Service() =>
        new(
            _db,
            Options.Create(new OperationsConfig()),
            new Fakes().Create<IHotelTextProvider>(),
            new ManualTimeProvider(new DateTimeOffset(NOW))
        );

    private async Task<List<CfhReportEntity>> Reports()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        return await db.CfhReports.AsNoTracking().ToListAsync(Ct);
    }

    private async Task Close(int id, bool sanctioned)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        var report = await db.CfhReports.SingleAsync(x => x.Id == id, Ct);
        report.ClosedAt = NOW.AddHours(-1);
        report.Sanctioned = sanctioned;
        await db.SaveChangesAsync(Ct);
    }

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };
}
