using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Guilds;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// A group's forum as the AS3 forum views use it: who may read, post, start threads, moderate
/// and change the settings (everybody / members / admins / owner, the refusal as
/// groupforum.view.error.&lt;error&gt;), messages numbered within the forum for the read marker,
/// hiding (10 by a moderator, 20 by staff) and restoring (1), sticky and locked threads, and the
/// player's side: the unread forum count, the lists and the read markers.
/// </summary>
public sealed class GuildForumTests : IDisposable
{
    private const int GROUP = 10;
    private const int PRIVATE_GROUP = 11;
    private const int OWNER = 1;
    private const int ADMIN = 2;
    private const int MEMBER = 3;
    private const int STRANGER = 4;
    private const int STAFF = 5;

    /// <summary>What the compose window sends for a new line.</summary>
    private const char LINE_BREAK = (char)13;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GuildForumTests()
    {
        foreach (var id in new[] { OWNER, ADMIN, MEMBER, STRANGER, STAFF })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"player{id}",
                    Figure = $"hd-180-{id}",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );

        foreach (var group in new[] { GROUP, PRIVATE_GROUP })
        {
            _db.Insert(
                new RoomEntity
                {
                    Id = group,
                    Name = "homeroom",
                    PlayerEntityId = OWNER,
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    UsersNow = 0,
                    PlayersMax = 25,
                    HideWalls = false,
                    AllowBlocking = false,
                    WallHeight = -1,
                    ThicknessWall = RoomThicknessType.Normal,
                    ThicknessFloor = RoomThicknessType.Normal,
                    AllowPets = true,
                    AllowPetsEat = true,
                    TradeType = RoomTradeModeType.Disabled,
                    MuteType = ModSettingType.Owner,
                    KickType = ModSettingType.Owner,
                    BanType = ModSettingType.Owner,
                    ChatFloodType = ChatFloodSensitivityType.Minimal,
                    PlayerEntity = null!,
                    RoomModelEntity = null!,
                }
            );
            _db.Insert(
                new GuildEntity
                {
                    Id = group,
                    Name = $"Group {group}",
                    Description = "A group",
                    BadgeCode = "b05114s19134",
                    PrimaryColorId = 1,
                    SecondaryColorId = 1,
                    GuildType = GuildType.Regular,
                    RightsLevel = GuildRightsLevel.Owner,
                    PlayerEntityId = OWNER,
                    RoomEntityId = group,
                }
            );

            foreach (
                var (player, rank) in new[]
                {
                    (OWNER, GuildMemberRank.Owner),
                    (ADMIN, GuildMemberRank.Admin),
                    (MEMBER, GuildMemberRank.Member),
                }
            )
                _db.Insert(
                    new GuildMemberEntity
                    {
                        Id = group * 10 + player,
                        GuildEntityId = group,
                        PlayerEntityId = player,
                        Rank = rank,
                        IsFavourite = false,
                    }
                );
        }

        _db.Insert(new GuildForumEntity { Id = 1, GuildEntityId = GROUP });
        _db.Insert(
            new GuildForumEntity
            {
                Id = 2,
                GuildEntityId = PRIVATE_GROUP,
                ReadPermission = GuildForumPermission.Members,
            }
        );

        _fakes.Handlers["GetMemberRankAsync"] = call =>
            Task.FromResult<GuildMemberRank?>(
                ((PlayerId)call.Args[0]!).Value switch
                {
                    OWNER => GuildMemberRank.Owner,
                    ADMIN => GuildMemberRank.Admin,
                    MEMBER => GuildMemberRank.Member,
                    _ => null,
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(Convert.ToInt64(call.Key) == STAFF);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_stranger_sees_what_they_may_not_do_and_why()
    {
        await (await ForumAsync()).SendForumAsync(STRANGER, Ct);

        var forum = Sent<ForumDataMessageComposer>(STRANGER).Single().Forum;
        forum.Name.Should().Be("Group 10");
        (forum.ReadError, forum.PostMessageError, forum.PostThreadError, forum.ModerateError)
            .Should()
            .Be(("", "not_member", "not_member", "not_admin"));
        forum.CanChangeSettings.Should().BeFalse();
        forum.IsStaff.Should().BeFalse();
    }

    [Fact]
    public async Task The_owner_may_change_the_settings_and_staff_everything()
    {
        await (await ForumAsync()).SendForumAsync(OWNER, Ct);
        await (await ForumAsync()).SendForumAsync(STAFF, Ct);

        Sent<ForumDataMessageComposer>(OWNER).Single().Forum.CanChangeSettings.Should().BeTrue();
        var staff = Sent<ForumDataMessageComposer>(STAFF).Single().Forum;
        (staff.ModerateError, staff.IsStaff).Should().Be(("", true));
    }

    [Fact]
    public async Task A_member_starts_a_thread_and_replies_numbered_within_the_forum()
    {
        var forum = await ForumAsync();

        await forum.PostAsync(MEMBER, 0, "A first subject", "A first message text", Ct);
        var thread = Sent<PostThreadMessageComposer>(MEMBER).Single().Thread;
        await (await ForumAsync()).PostAsync(
            ADMIN,
            thread.ThreadId,
            "",
            "A reply to the first",
            Ct
        );

        (thread.Subject, thread.AuthorName, thread.TotalMessages, thread.LastMessageId)
            .Should()
            .Be(("A first subject", "player3", 1, 1));
        var reply = Sent<PostMessageMessageComposer>(ADMIN).Single().Message;
        (reply.MessageId, reply.MessageIndex, reply.AuthorName, reply.AuthorPostCount)
            .Should()
            .Be((2, 1, "player2", 1));
    }

    [Fact]
    public async Task A_stranger_may_not_post_and_is_told()
    {
        await (await ForumAsync()).PostAsync(STRANGER, 0, "A first subject", "Some text here", Ct);

        Sent<PostThreadMessageComposer>(STRANGER).Should().BeEmpty();
        Notices(STRANGER).Should().Equal(GuildForumNotificationTypes.ACCESS_DENIED);
        _db.CreateDbContext().GuildForumThreads.Should().BeEmpty();
    }

    [Fact]
    public async Task Too_short_a_post_and_a_post_within_the_cooldown_are_refused()
    {
        var forum = await ForumAsync();

        await forum.PostAsync(MEMBER, 0, "short", "A first message text", Ct);
        await forum.PostAsync(MEMBER, 0, "A first subject", "A first message text", Ct);
        await forum.PostAsync(MEMBER, 0, "A second subject", "A second message text", Ct);

        Sent<PostThreadMessageComposer>(MEMBER).Should().ContainSingle();
    }

    [Fact]
    public async Task Pinned_threads_come_first_and_unread_counts_follow_the_read_marker()
    {
        var first = await PostThreadAsync(MEMBER, "A first subject");
        var second = await PostThreadAsync(ADMIN, "A second subject");
        await (await ForumAsync()).UpdateThreadAsync(ADMIN, first, true, false, Ct);
        await PlayerForums(STRANGER)
            .MarkReadAsync(
                [
                    new()
                    {
                        GroupId = GROUP,
                        LastReadMessageId = 1,
                        MarkAll = false,
                    },
                ],
                Ct
            );

        await (await ForumAsync()).SendThreadsAsync(STRANGER, 0, 20, Ct);

        Sent<ForumThreadsMessageComposer>(STRANGER)
            .Single()
            .Threads.Select(x => (x.ThreadId, x.IsSticky, x.UnreadMessages))
            .Should()
            .Equal((first, true, 0), (second, false, 1));
        Notices(ADMIN).Should().Contain(GuildForumNotificationTypes.THREAD_PINNED);
    }

    [Fact]
    public async Task A_hidden_thread_is_masked_for_whoever_may_not_moderate()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");

        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 10, Ct);
        await (await ForumAsync()).SendThreadsAsync(STRANGER, 0, 20, Ct);
        await (await ForumAsync()).SendThreadsAsync(OWNER, 0, 20, Ct);

        Sent<UpdateThreadMessageComposer>(ADMIN)
            .Single()
            .Thread.Should()
            .Match<GuildForumThreadSnapshot>(x =>
                x.State == GuildForumState.HiddenByAdmin && x.ModeratorName == "player2"
            );
        Notices(ADMIN).Should().Equal(GuildForumNotificationTypes.THREAD_HIDDEN);
        Sent<ForumThreadsMessageComposer>(STRANGER).Single().Threads[0].Subject.Should().BeEmpty();
        Sent<ForumThreadsMessageComposer>(OWNER)
            .Single()
            .Threads[0]
            .Subject.Should()
            .Be("A first subject");
    }

    [Fact]
    public async Task Only_staff_hide_with_20_and_restore_what_staff_hid()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");

        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 20, Ct);
        await (await ForumAsync()).ModerateThreadAsync(STAFF, thread, 20, Ct);
        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 1, Ct);
        await (await ForumAsync()).ModerateThreadAsync(MEMBER, thread, 10, Ct);

        Notices(ADMIN)
            .Should()
            .Equal(
                GuildForumNotificationTypes.ACCESS_DENIED,
                GuildForumNotificationTypes.ACCESS_DENIED
            );
        Notices(MEMBER).Should().Equal(GuildForumNotificationTypes.ACCESS_DENIED);
        Sent<UpdateThreadMessageComposer>(STAFF)
            .Single()
            .Thread.State.Should()
            .Be(GuildForumState.HiddenByStaff);
    }

    [Fact]
    public async Task A_moderator_cannot_undo_a_staff_hide_by_hiding_it_again_first()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");

        await (await ForumAsync()).ModerateThreadAsync(STAFF, thread, 20, Ct);
        await (await ForumAsync()).ModerateMessageAsync(STAFF, thread, 1, 20, Ct);
        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 10, Ct);
        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 1, Ct);
        await (await ForumAsync()).ModerateMessageAsync(ADMIN, thread, 1, 10, Ct);
        await (await ForumAsync()).ModerateMessageAsync(ADMIN, thread, 1, 1, Ct);
        await (await ForumAsync()).SendThreadsAsync(STAFF, 0, 20, Ct);
        await (await ForumAsync()).SendMessagesAsync(STAFF, thread, 0, 20, Ct);

        Notices(ADMIN)
            .Should()
            .Equal(Enumerable.Repeat(GuildForumNotificationTypes.ACCESS_DENIED, 4));
        Sent<ForumThreadsMessageComposer>(STAFF)
            .Single()
            .Threads[0]
            .State.Should()
            .Be(GuildForumState.HiddenByStaff);
        Sent<ThreadMessagesMessageComposer>(STAFF)
            .Single()
            .Messages[0]
            .State.Should()
            .Be(GuildForumState.HiddenByStaff);
    }

    [Fact]
    public async Task A_hidden_threads_messages_are_not_sent_to_whoever_may_not_see_it()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");

        await (await ForumAsync()).ModerateThreadAsync(ADMIN, thread, 10, Ct);
        await (await ForumAsync()).SendMessagesAsync(MEMBER, thread, 0, 20, Ct);
        await (await ForumAsync()).SendMessagesAsync(ADMIN, thread, 0, 20, Ct);

        Sent<ThreadMessagesMessageComposer>(MEMBER).Should().BeEmpty();
        Sent<ThreadMessagesMessageComposer>(ADMIN)
            .Single()
            .Messages[0]
            .Text.Should()
            .Be("A message long enough");
    }

    [Fact]
    public async Task A_forum_whose_group_was_deleted_serves_nothing()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");
        var forum = await ForumAsync();

        await forum.OnGuildDeletedAsync(Ct);
        await forum.SendForumAsync(MEMBER, Ct);
        await forum.SendThreadsAsync(MEMBER, 0, 20, Ct);
        await forum.SendMessagesAsync(MEMBER, thread, 0, 20, Ct);

        Sent<ForumDataMessageComposer>(MEMBER).Should().BeEmpty();
        Sent<ForumThreadsMessageComposer>(MEMBER).Should().BeEmpty();
        Sent<ThreadMessagesMessageComposer>(MEMBER).Should().BeEmpty();
    }

    [Fact]
    public async Task A_hidden_message_keeps_its_text_from_members_but_not_from_moderators()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");

        await (await ForumAsync()).ModerateMessageAsync(ADMIN, thread, 1, 10, Ct);
        await (await ForumAsync()).SendMessagesAsync(MEMBER, thread, 0, 20, Ct);
        await (await ForumAsync()).SendMessagesAsync(ADMIN, thread, 0, 20, Ct);

        Notices(ADMIN).Should().Equal(GuildForumNotificationTypes.MESSAGE_HIDDEN);
        Sent<ThreadMessagesMessageComposer>(MEMBER)
            .Single()
            .Messages[0]
            .Should()
            .Match<GuildForumMessageSnapshot>(x =>
                x.Text == "" && x.State == GuildForumState.HiddenByAdmin
            );
        Sent<ThreadMessagesMessageComposer>(ADMIN)
            .Single()
            .Messages[0]
            .Text.Should()
            .Be("A message long enough");
    }

    [Fact]
    public async Task A_locked_thread_takes_replies_only_from_moderators()
    {
        var thread = await PostThreadAsync(MEMBER, "A first subject");
        await (await ForumAsync()).UpdateThreadAsync(ADMIN, thread, false, true, Ct);

        await (await ForumAsync()).PostAsync(MEMBER, thread, "", "A reply in a lock", Ct);
        await (await ForumAsync()).PostAsync(OWNER, thread, "", "A reply in a lock", Ct);

        Sent<PostMessageMessageComposer>(MEMBER).Should().BeEmpty();
        Sent<PostMessageMessageComposer>(OWNER).Should().ContainSingle();
        Notices(ADMIN).Should().Equal(GuildForumNotificationTypes.THREAD_LOCKED);
    }

    [Fact]
    public async Task Settings_are_the_owners_and_must_build_on_each_other()
    {
        await (await ForumAsync()).UpdateSettingsAsync(ADMIN, 1, 1, 1, 2, Ct);
        await (await ForumAsync()).UpdateSettingsAsync(OWNER, 1, 0, 1, 2, Ct);
        await (await ForumAsync()).UpdateSettingsAsync(OWNER, 1, 1, 2, 3, Ct);

        Notices(ADMIN).Should().Equal(GuildForumNotificationTypes.ACCESS_DENIED);
        Notices(OWNER)
            .Should()
            .Equal(
                GuildForumNotificationTypes.ACCESS_DENIED,
                GuildForumNotificationTypes.SETTINGS_UPDATED
            );
        var forum = Sent<ForumDataMessageComposer>(OWNER).Single().Forum;
        (
            forum.ReadPermission,
            forum.PostMessagePermission,
            forum.PostThreadPermission,
            forum.ModeratePermission
        )
            .Should()
            .Be(
                (
                    GuildForumPermission.Members,
                    GuildForumPermission.Members,
                    GuildForumPermission.Admins,
                    GuildForumPermission.Owner
                )
            );
    }

    [Fact]
    public async Task A_members_only_forum_cannot_be_read_by_a_stranger()
    {
        await (await ForumAsync(PRIVATE_GROUP)).SendThreadsAsync(STRANGER, 0, 20, Ct);

        Sent<ForumThreadsMessageComposer>(STRANGER).Should().BeEmpty();
        Notices(STRANGER).Should().Equal(GuildForumNotificationTypes.ACCESS_DENIED);
    }

    [Fact]
    public async Task The_owner_buying_a_terminal_opens_the_forum_once_and_says_so()
    {
        using (var db = _db.CreateDbContext())
        {
            db.GuildForums.RemoveRange(db.GuildForums);
            db.SaveChanges();
        }

        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult<GuildSnapshot?>(
                new(
                    new GuildSummarySnapshot
                    {
                        GuildId = GuildId.Parse(GROUP),
                        Name = "Group 10",
                        BadgeCode = "",
                        RoomId = GROUP,
                        OwnerId = OWNER,
                        PrimaryColorId = 1,
                        SecondaryColorId = 1,
                        PrimaryColor = "",
                        SecondaryColor = "",
                        Type = GuildType.Regular,
                        HasForum = false,
                        RightsLevel = GuildRightsLevel.Owner,
                    }
                )
                {
                    Description = "",
                    CreatedAt = DateTime.UtcNow,
                }
            );

        (await (await ForumAsync()).OpenAsync(MEMBER, Ct)).Should().BeFalse();
        var forum = await ForumAsync();
        (await forum.OpenAsync(OWNER, Ct)).Should().BeTrue();
        (await forum.OpenAsync(OWNER, Ct)).Should().BeFalse();

        _db.CreateDbContext().GuildForums.Select(x => x.GuildEntityId).Should().Equal(GROUP);
        _fakes.Log.Of("OnForumOpenedAsync").Should().ContainSingle();
        var notice = Sent<NotificationDialogMessageComposer>(OWNER).Single();
        notice.NotificationType.Should().Be(GuildForumNotificationTypes.DELIVERED);
        notice.Parameters["GROUPNAME"].Should().Be("Group 10");
    }

    [Fact]
    public async Task The_unread_count_is_the_players_forums_with_messages_past_their_marker()
    {
        await PostThreadAsync(MEMBER, "A first subject");

        await PlayerForums(ADMIN).SendUnreadForumsCountAsync(Ct);
        await PlayerForums(ADMIN)
            .MarkReadAsync(
                [
                    new()
                    {
                        GroupId = GROUP,
                        LastReadMessageId = 0,
                        MarkAll = true,
                    },
                ],
                Ct
            );
        await PlayerForums(ADMIN).SendUnreadForumsCountAsync(Ct);
        await PlayerForums(STRANGER).SendUnreadForumsCountAsync(Ct);

        Sent<UnreadForumsCountMessageComposer>(ADMIN)
            .Select(x => x.UnreadForumsCount)
            .Should()
            .Equal(1, 0);
        Sent<UnreadForumsCountMessageComposer>(STRANGER).Single().UnreadForumsCount.Should().Be(0);
    }

    [Fact]
    public async Task A_read_marker_never_goes_back_nor_past_the_last_message()
    {
        await PostThreadAsync(MEMBER, "A first subject");
        var forums = PlayerForums(STRANGER);

        await forums.MarkReadAsync(
            [
                new()
                {
                    GroupId = GROUP,
                    LastReadMessageId = 9,
                    MarkAll = false,
                },
            ],
            Ct
        );
        await forums.MarkReadAsync(
            [
                new()
                {
                    GroupId = GROUP,
                    LastReadMessageId = 0,
                    MarkAll = false,
                },
            ],
            Ct
        );

        _db.CreateDbContext().GuildForumReadMarkers.Single().LastReadMessageId.Should().Be(1);
    }

    [Fact]
    public async Task Reading_a_thread_marks_its_messages_read_and_updates_the_counter()
    {
        var first = await PostThreadAsync(MEMBER, "A first subject");
        await PostThreadAsync(ADMIN, "A second subject");
        _fakes.Handlers["MarkReadAsync"] = call =>
            PlayerForums((int)Convert.ToInt64(call.Key))
                .MarkReadAsync((ImmutableArray<GuildForumReadMarkerSnapshot>)call.Args[0]!, Ct);

        await (await ForumAsync()).SendMessagesAsync(OWNER, first, 0, 20, Ct);

        _db.CreateDbContext()
            .GuildForumReadMarkers.Single(x => x.PlayerEntityId == OWNER)
            .LastReadMessageId.Should()
            .Be(1);
        _fakes
            .Log.Of("SendUnreadForumsCountAsync")
            .Select(x => Convert.ToInt64(x.Key))
            .Should()
            .Equal(OWNER);
    }

    [Fact]
    public async Task A_line_break_in_a_post_comes_back_as_it_was_sent()
    {
        var text = $"First line{LINE_BREAK}second line";
        var forum = await ForumAsync();
        await forum.PostAsync(MEMBER, 0, "A first subject", text, Ct);
        var thread = Sent<PostThreadMessageComposer>(MEMBER).Single().Thread.ThreadId;

        await (await ForumAsync()).SendMessagesAsync(MEMBER, thread, 0, 20, Ct);

        Sent<ThreadMessagesMessageComposer>(MEMBER).Single().Messages[0].Text.Should().Be(text);
    }

    [Fact]
    public async Task My_forums_are_my_groups_and_the_most_lists_only_public_ones()
    {
        await PostThreadAsync(MEMBER, "A first subject");

        await PlayerForums(MEMBER).SendForumsListAsync(2, 0, 20, Ct);
        await PlayerForums(MEMBER).SendForumsListAsync(0, 0, 20, Ct);

        var lists = Sent<ForumsListMessageComposer>(MEMBER).ToList();
        lists[0].Forums.Select(x => x.GroupId).Should().Equal(GROUP, PRIVATE_GROUP);
        lists[0].TotalAmount.Should().Be(2);
        lists[0]
            .Forums[0]
            .Should()
            .Match<GuildForumSnapshot>(x =>
                x.TotalMessages == 1
                && x.UnreadMessages == 1
                && x.LeaderboardScore == 1
                && x.LastMessageAuthorName == "player3"
            );
        (lists[1].ListCode, lists[1].TotalAmount).Should().Be((GuildForumListType.MostActive, 1));
    }

    private async Task<int> PostThreadAsync(int author, string subject)
    {
        await (await ForumAsync()).PostAsync(author, 0, subject, "A message long enough", Ct);

        return Sent<PostThreadMessageComposer>(author).Last().Thread.ThreadId;
    }

    private async Task<IGuildForumGrain> ForumAsync(int group = GROUP)
    {
        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.GuildForumGrain",
            _fakes,
            _db
        );
        RoomHarness.SetMember(
            grain
                .GetType()
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(grain)!,
            "GuildId",
            GuildId.Parse(group)
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IGuildForumGrain)grain;
    }

    private IPlayerGuildForumGrain PlayerForums(int player)
    {
        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.PlayerGuildForumGrain",
            _fakes,
            _db
        );
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            .GetSetMethod(true)!
            .Invoke(
                grain,
                [
                    GrainContextStub.Create(
                        GrainId.Create(
                            GrainType.Create("playerguildforum"),
                            GrainIdKeyExtensions.CreateIntegerKey(player)
                        )
                    ),
                ]
            );

        return (IPlayerGuildForumGrain)grain;
    }

    private IEnumerable<T> Sent<T>(int player)
        where T : IComposer =>
        _fakes
            .Log.Of("SendComposerAsync")
            .Where(x => Convert.ToInt64(x.Key) == player)
            .Select(x => x.Args[0])
            .OfType<T>();

    private IEnumerable<string> Notices(int player) =>
        Sent<NotificationDialogMessageComposer>(player).Select(x => x.NotificationType);
}
