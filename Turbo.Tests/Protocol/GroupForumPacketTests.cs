using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The group forum packets in the order the AS3 groupforums parsers and composers use them:
/// ForumData (plus the ExtendedForumData permissions), ThreadData, MessageData, the lists, and
/// the requests, UpdateThread putting sticky before locked and the read marker a counted list.
/// </summary>
public class GroupForumPacketTests
{
    private static readonly GuildForumSnapshot Forum = new()
    {
        GroupId = 10,
        Name = "Group",
        Description = "About",
        Icon = "b05114s19134",
        TotalThreads = 2,
        LeaderboardScore = 3,
        TotalMessages = 4,
        UnreadMessages = 1,
        LastMessageId = 4,
        LastMessageAuthorId = 7,
        LastMessageAuthorName = "alice",
        LastMessageSecondsAgo = 60,
    };

    [Fact]
    public void forum_data_is_the_forum_then_its_permissions_errors_and_flags()
    {
        var reply = PacketHarness.Encode(
            new ForumDataMessageComposer
            {
                Forum = new(Forum)
                {
                    ReadPermission = GuildForumPermission.Everybody,
                    PostMessagePermission = GuildForumPermission.Members,
                    PostThreadPermission = GuildForumPermission.Admins,
                    ModeratePermission = GuildForumPermission.Owner,
                    ReadError = "",
                    PostMessageError = "not_member",
                    PostThreadError = "not_admin",
                    ModerateError = "not_owner",
                    ReportError = "",
                    CanChangeSettings = false,
                    IsStaff = true,
                },
            }
        );

        AssertForum(reply);
        Assert.Equal(
            [0, 1, 2, 3],
            new[] { reply.PopInt(), reply.PopInt(), reply.PopInt(), reply.PopInt() }
        );
        Assert.Equal(
            ["", "not_member", "not_admin", "not_owner", ""],
            new[]
            {
                reply.PopString(),
                reply.PopString(),
                reply.PopString(),
                reply.PopString(),
                reply.PopString(),
            }
        );
        Assert.False(reply.PopBoolean());
        Assert.True(reply.PopBoolean());
        Assert.True(reply.End);
    }

    [Fact]
    public void a_list_is_code_total_start_and_forums()
    {
        var reply = PacketHarness.Encode(
            new ForumsListMessageComposer
            {
                ListCode = GuildForumListType.MyForums,
                TotalAmount = 5,
                StartIndex = 0,
                Forums = [Forum],
            }
        );

        Assert.Equal(2, reply.PopInt());
        Assert.Equal(5, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal(1, reply.PopInt());
        AssertForum(reply);
        Assert.True(reply.End);
    }

    [Fact]
    public void threads_and_messages_are_written_as_ThreadData_and_MessageData()
    {
        var threads = PacketHarness.Encode(
            new ForumThreadsMessageComposer
            {
                GroupId = 10,
                StartIndex = 20,
                Threads =
                [
                    new()
                    {
                        ThreadId = 3,
                        AuthorId = 7,
                        AuthorName = "alice",
                        Subject = "Subject",
                        IsSticky = true,
                        IsLocked = false,
                        CreatedSecondsAgo = 100,
                        TotalMessages = 2,
                        UnreadMessages = 1,
                        LastMessageId = 4,
                        LastMessageAuthorId = 8,
                        LastMessageAuthorName = "bob",
                        LastMessageSecondsAgo = 50,
                        State = GuildForumState.HiddenByAdmin,
                        ModeratorId = 9,
                        ModeratorName = "mod",
                        ModeratedSecondsAgo = 10,
                    },
                ],
            }
        );

        Assert.Equal(10, threads.PopInt());
        Assert.Equal(20, threads.PopInt());
        Assert.Equal(1, threads.PopInt());
        Assert.Equal(3, threads.PopInt());
        Assert.Equal(7, threads.PopInt());
        Assert.Equal("alice", threads.PopString());
        Assert.Equal("Subject", threads.PopString());
        Assert.True(threads.PopBoolean());
        Assert.False(threads.PopBoolean());
        Assert.Equal(100, threads.PopInt());
        Assert.Equal(2, threads.PopInt());
        Assert.Equal(1, threads.PopInt());
        Assert.Equal(4, threads.PopInt());
        Assert.Equal(8, threads.PopInt());
        Assert.Equal("bob", threads.PopString());
        Assert.Equal(50, threads.PopInt());
        Assert.Equal(10, threads.PopByte());
        Assert.Equal(9, threads.PopInt());
        Assert.Equal("mod", threads.PopString());
        Assert.Equal(10, threads.PopInt());
        Assert.True(threads.End);

        var messages = PacketHarness.Encode(
            new ThreadMessagesMessageComposer
            {
                GroupId = 10,
                ThreadId = 3,
                StartIndex = 0,
                Messages =
                [
                    new()
                    {
                        MessageId = 4,
                        MessageIndex = 1,
                        AuthorId = 8,
                        AuthorName = "bob",
                        AuthorFigure = "hd-180-1",
                        CreatedSecondsAgo = 50,
                        Text = "Hello",
                        State = GuildForumState.Normal,
                        ModeratorId = 0,
                        ModeratorName = "",
                        ModeratedSecondsAgo = 0,
                        AuthorPostCount = 12,
                    },
                ],
            }
        );

        Assert.Equal(10, messages.PopInt());
        Assert.Equal(3, messages.PopInt());
        Assert.Equal(0, messages.PopInt());
        Assert.Equal(1, messages.PopInt());
        Assert.Equal(4, messages.PopInt());
        Assert.Equal(1, messages.PopInt());
        Assert.Equal(8, messages.PopInt());
        Assert.Equal("bob", messages.PopString());
        Assert.Equal("hd-180-1", messages.PopString());
        Assert.Equal(50, messages.PopInt());
        Assert.Equal("Hello", messages.PopString());
        Assert.Equal(0, messages.PopByte());
        Assert.Equal(0, messages.PopInt());
        Assert.Equal("", messages.PopString());
        Assert.Equal(0, messages.PopInt());
        Assert.Equal(12, messages.PopInt());
        Assert.True(messages.End);
    }

    [Fact]
    public void update_thread_reads_sticky_before_locked()
    {
        var message = (UpdateThreadMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("UpdateThreadMessageEvent"),
                PacketHarness.Payload(w => w.Int(10).Int(3).Bool(true).Bool(false))
            );

        Assert.Equal(
            (10, 3, true, false),
            (message.GroupId, message.ThreadId, message.IsSticky, message.IsLocked)
        );
    }

    [Fact]
    public void the_read_marker_is_a_counted_list_and_a_post_carries_subject_then_text()
    {
        var markers = (UpdateForumReadMarkerMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("UpdateForumReadMarkerMessageEvent"),
                PacketHarness.Payload(w =>
                    w.Int(2).Int(10).Int(4).Bool(false).Int(11).Int(9).Bool(true)
                )
            );
        var post = (PostMessageMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("PostMessageMessageEvent"),
                PacketHarness.Payload(w => w.Int(10).Int(0).String("Subject").String("Text"))
            );

        Assert.Equal(
            [
                new GuildForumReadMarkerSnapshot
                {
                    GroupId = 10,
                    LastReadMessageId = 4,
                    MarkAll = false,
                },
                new GuildForumReadMarkerSnapshot
                {
                    GroupId = 11,
                    LastReadMessageId = 9,
                    MarkAll = true,
                },
            ],
            markers.Markers
        );
        Assert.Equal(
            (10, 0, "Subject", "Text"),
            (post.GroupId, post.ThreadId, post.Subject, post.Text)
        );
    }

    [Fact]
    public async Task requests_reach_the_forum_and_the_players_forums()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(PacketHarness.Incoming("GetUnreadForumsCountMessageEvent"), []);
        await harness.SendAsync(
            PacketHarness.Incoming("GetThreadsMessageEvent"),
            PacketHarness.Payload(w => w.Int(10).Int(20).Int(20))
        );

        Assert.Single(harness.Fakes.Log.Of("SendUnreadForumsCountAsync"));
        var threads = Assert.Single(harness.Fakes.Log.Of("SendThreadsAsync"));
        Assert.Equal(10L, Convert.ToInt64(threads.Key));
        Assert.Equal(20, threads.Args[1]);
        Assert.Equal(20, threads.Args[2]);
    }

    private static void AssertForum(ClientPacket reply)
    {
        Assert.Equal(10, reply.PopInt());
        Assert.Equal("Group", reply.PopString());
        Assert.Equal("About", reply.PopString());
        Assert.Equal("b05114s19134", reply.PopString());
        Assert.Equal(2, reply.PopInt());
        Assert.Equal(3, reply.PopInt());
        Assert.Equal(4, reply.PopInt());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(4, reply.PopInt());
        Assert.Equal(7, reply.PopInt());
        Assert.Equal("alice", reply.PopString());
        Assert.Equal(60, reply.PopInt());
    }
}
