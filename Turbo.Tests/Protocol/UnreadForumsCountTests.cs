using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The client's <c>GroupForumController</c> asks for the unread forum count at login and every
/// <c>groupforum.poll.period</c>, and keeps the answer it last got. No group has a forum yet, so
/// the honest answer is none.
/// </summary>
public class UnreadForumsCountTests
{
    [Fact]
    public async Task asking_for_the_unread_forum_count_is_answered_with_none()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetUnreadForumsCountMessageEvent"),
            []
        );

        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("UnreadForumsCountMessageComposer"), reply.Header);
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }
}
