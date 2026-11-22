using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Import.MorningstarImportPlayersTests;

namespace Turbo.Tests.Import;

/// <summary>
/// A Morningstar hotel's rooms and what stands in them besides furniture (groups, pets, bots),
/// imported into an empty Turbo database.
/// </summary>
public class MorningstarImportRoomsTests
{
    private const string MODEL_A = "xxxxxx\r\nx0000x\r\nx0000x\r\nxxxxxx";

    private static MorningstarDb Hotel()
    {
        var ms = MorningstarImportPlayersTests.Hotel();

        ms.Execute("UPDATE users SET home_room = 100 WHERE id = 10");
        ms.Insert(
                "navigator_flatcats",
                new
                {
                    id = 4,
                    min_rank = 1,
                    caption_save = "chat",
                    caption = "Chat and discussion",
                    order_num = 2,
                }
            )
            .Insert(
                "navigator_flatcats",
                new
                {
                    id = 9,
                    min_rank = 1,
                    caption_save = "rp",
                    caption = "Role Playing",
                    order_num = 3,
                }
            )
            .Insert(
                "room_models",
                new
                {
                    name = "model_a",
                    door_x = 1,
                    door_y = 1,
                    door_dir = 2,
                    heightmap = MODEL_A,
                }
            )
            .Insert(
                "room_models",
                new
                {
                    name = "model_bad",
                    door_x = 0,
                    door_y = 0,
                    door_dir = 2,
                    heightmap = "x0?0",
                }
            )
            .Insert(
                "room_models_custom",
                new
                {
                    id = 100,
                    name = "custom_100",
                    door_x = 1,
                    door_y = 1,
                    door_dir = 4,
                    heightmap = "xxx\rx12\rx3a",
                }
            )
            .Insert(
                "rooms",
                new
                {
                    id = 100,
                    owner_id = 10,
                    owner_name = "Alice",
                    name = "Alice's place",
                    description = "Come in",
                    model = "model_a",
                    password = "",
                    state = "locked",
                    users_max = 30,
                    category = 4,
                    score = 12,
                    paper_floor = "0.0",
                    paper_wall = "1501",
                    paper_landscape = "0.0",
                    thickness_wall = -1,
                    wall_height = -1,
                    thickness_floor = 1,
                    tags = "party;chill;",
                    is_staff_picked = "1",
                    allow_other_pets = "1",
                    allow_walkthrough = "0",
                    who_can_kick = 2,
                    override_model = "1",
                    trade_mode = 0,
                    moodlight_data = "2,1,2,#000000,255;1,2,1,#FF00FF,120;",
                }
            )
            .Insert(
                "rooms",
                new
                {
                    id = 101,
                    owner_id = 11,
                    name = "Bob's",
                    model = "model_unknown",
                    state = "password",
                    password = "secret",
                    override_model = "0",
                    users_max = 10,
                }
            )
            .Insert(
                "rooms",
                new
                {
                    id = 102,
                    owner_id = 999,
                    name = "Orphan",
                    model = "model_a",
                    state = "open",
                }
            )
            .Insert("room_rights", new { room_id = 100, user_id = 11 })
            .Insert("room_rights", new { room_id = 100, user_id = 10 })
            .Insert(
                "room_bans",
                new
                {
                    room_id = 100,
                    user_id = 12,
                    ends = Unix(NOW.AddHours(1)),
                }
            )
            .Insert(
                "room_bans",
                new
                {
                    room_id = 100,
                    user_id = 11,
                    ends = Unix(NOW.AddHours(-1)),
                }
            )
            .Insert("room_votes", new { user_id = 11, room_id = 100 })
            .Insert("room_wordfilter", new { room_id = 100, word = "bobba" })
            .Insert("users_favorite_rooms", new { user_id = 11, room_id = 100 })
            .Insert("users_favorite_rooms", new { user_id = 11, room_id = 102 })
            .Insert(
                "guilds",
                new
                {
                    id = 5,
                    user_id = 10,
                    name = "Alice's group",
                    description = "Fans",
                    room_id = 100,
                    state = 1,
                    rights = "1",
                    color_one = 3,
                    color_two = 11,
                    badge = "b0503Xs09114",
                    date_created = Unix(NOW.AddDays(-30)),
                    forum = "1",
                    read_forum = "MEMBERS",
                    post_messages = "MEMBERS",
                    post_threads = "ADMINS",
                    mod_forum = "OWNER",
                }
            )
            .Insert(
                "guilds_members",
                new
                {
                    id = 1,
                    guild_id = 5,
                    user_id = 10,
                    level_id = 0,
                    member_since = Unix(NOW.AddDays(-30)),
                }
            )
            .Insert(
                "guilds_members",
                new
                {
                    id = 2,
                    guild_id = 5,
                    user_id = 11,
                    level_id = 3,
                    member_since = Unix(NOW.AddDays(-2)),
                }
            )
            .Insert(
                "guilds_forums_threads",
                new
                {
                    id = 7,
                    guild_id = 5,
                    opener_id = 10,
                    subject = "Welcome",
                    created_at = Unix(NOW.AddDays(-3)),
                    updated_at = Unix(NOW.AddDays(-1)),
                    state = 0,
                    pinned = 1,
                    locked = 0,
                    admin_id = 0,
                }
            )
            .Insert(
                "guilds_forums_comments",
                new
                {
                    id = 70,
                    thread_id = 7,
                    user_id = 10,
                    message = "Hi all",
                    created_at = Unix(NOW.AddDays(-3)),
                    state = 0,
                    admin_id = 0,
                }
            )
            .Insert(
                "guilds_forums_comments",
                new
                {
                    id = 71,
                    thread_id = 7,
                    user_id = 11,
                    message = "Hey",
                    created_at = Unix(NOW.AddDays(-1)),
                    state = 20,
                    admin_id = 10,
                }
            )
            .Insert(
                "users_pets",
                new
                {
                    id = 300,
                    user_id = 10,
                    room_id = 100,
                    name = "Spirit",
                    race = 3,
                    type = PetTypes.HORSE,
                    color = "ffffff",
                    experience = 450,
                    energy = 80,
                    hunger = 30,
                    respect = 4,
                    created = Unix(NOW.AddDays(-40)),
                    x = 2,
                    y = 1,
                    z = 0.0,
                    rot = 4,
                    hair_style = 12,
                    hair_color = 5,
                    saddle = "1",
                    ride = "1",
                }
            )
            .Insert(
                "users_pets",
                new
                {
                    id = 301,
                    user_id = 11,
                    room_id = 555,
                    name = "Rex",
                    race = 1,
                    type = PetTypes.DOG,
                    color = "FF0000",
                    experience = 0,
                    created = Unix(NOW.AddDays(-1)),
                }
            )
            .Insert(
                "bots",
                new
                {
                    id = 400,
                    user_id = 10,
                    room_id = 100,
                    name = "Frank",
                    motto = "Hi",
                    figure = "hr-1",
                    gender = "M",
                    x = 1,
                    y = 2,
                    z = 0.0,
                    rot = 2,
                    chat_lines = "Hello\rWelcome!\r",
                    chat_auto = "1",
                    chat_random = "0",
                    chat_delay = 7,
                    dance = 2,
                    freeroam = "1",
                    type = "bartender",
                }
            )
            .Insert(
                "bots",
                new
                {
                    id = 401,
                    user_id = 999,
                    room_id = 0,
                    name = "Ghost",
                    figure = "hr-1",
                    gender = "F",
                    type = "generic",
                }
            );

        return ms;
    }

    [Fact]
    public async Task Rooms_keep_their_settings_floor_plans_and_rights()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        var report = await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var room = await db.Rooms.SingleAsync(x => x.Id == 100, ct);
        var plan = await db.RoomModels.SingleAsync(x => x.Id == room.RoomModelEntityId, ct);

        // Its own floor plan, named as Turbo's editor names one, rows Turbo reads.
        Assert.Equal("room_100", plan.Name);
        Assert.True(plan.Custom);
        Assert.Equal("xxx\r\nx12\r\nx3a", plan.Model);
        Assert.Equal(Rotation.South, plan.DoorRotation);

        Assert.Equal(RoomDoorModeType.Locked, room.DoorMode);
        Assert.Equal(30, room.PlayersMax);
        Assert.Null(room.PaintFloor);
        Assert.Equal("1501", room.PaintWall);
        Assert.Equal(RoomThicknessType.Thick, room.ThicknessFloor);
        Assert.Equal("party,chill", room.Tags);
        Assert.True(room.StaffPick);
        Assert.False(room.AllowBlocking);
        Assert.Equal(ModSettingType.All, room.KickType);
        Assert.Equal(RoomTradeModeType.Disabled, room.TradeType);
        Assert.Equal(12, room.Score);

        // Categories are matched by name: Turbo had none, so both are added and mapped.
        var category = await db.NavigatorFlatCategories.SingleAsync(
            x => x.Id == room.NavigatorCategoryEntityId,
            ct
        );

        Assert.Equal("Chat and discussion", category.Name);

        // A model neither hotel has falls back to model_a; a room without its owner stays behind.
        var bobs = await db.Rooms.SingleAsync(x => x.Id == 101, ct);

        Assert.Equal(
            "model_a",
            (await db.RoomModels.SingleAsync(x => x.Id == bobs.RoomModelEntityId, ct)).Name
        );
        Assert.Equal(RoomDoorModeType.Password, bobs.DoorMode);
        Assert.Equal("secret", bobs.Password);
        Assert.False(await db.Rooms.AnyAsync(x => x.Id == 102, ct));
        Assert.False(await db.RoomModels.AnyAsync(x => x.Name == "model_bad", ct));
        Assert.Equal(1, report.NotesOf("room left out: its owner was not copied"));

        // The owner's own rights row is not one Turbo keeps; the expired ban is gone.
        Assert.Equal([11], await db.RoomRights.Select(x => x.PlayerEntityId).ToListAsync(ct));
        Assert.Equal([12], await db.RoomBans.Select(x => x.PlayerEntityId).ToListAsync(ct));
        Assert.Equal(1, (await db.RoomRatings.SingleAsync(ct)).Rating);
        Assert.Equal("bobba", (await db.RoomFilterWords.SingleAsync(ct)).Word);
        Assert.Equal(
            [100],
            await db.PlayerFavouriteRooms.Select(x => x.RoomEntityId).ToListAsync(ct)
        );

        // users.home_room, the one Morningstar reads.
        Assert.Equal(
            100,
            (await db.PlayerSettings.SingleAsync(x => x.PlayerEntityId == 10, ct)).HomeRoomId
        );
    }

    [Fact]
    public async Task Groups_keep_their_members_settings_and_forum()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var guild = await db.Guilds.SingleAsync(ct);

        Assert.Equal((5, 100, 10), (guild.Id, guild.RoomEntityId, guild.PlayerEntityId));
        Assert.Equal(GuildType.Exclusive, guild.GuildType);
        Assert.Equal(GuildRightsLevel.Members, guild.RightsLevel);
        Assert.Equal("b0503Xs09114", guild.BadgeCode);
        Assert.Equal((3, 11), (guild.PrimaryColorId, guild.SecondaryColorId));

        var members = await db.GuildMembers.OrderBy(x => x.PlayerEntityId).ToListAsync(ct);

        Assert.Equal(
            [GuildMemberRank.Owner, GuildMemberRank.Requested],
            members.Select(x => x.Rank)
        );

        var forum = await db.GuildForums.SingleAsync(ct);

        Assert.Equal(GuildForumPermission.Members, forum.ReadPermission);
        Assert.Equal(GuildForumPermission.Admins, forum.PostThreadPermission);
        Assert.Equal(GuildForumPermission.Owner, forum.ModeratePermission);
        Assert.Equal((1, 2), (forum.ThreadCount, forum.MessageCount));

        var thread = await db.GuildForumThreads.SingleAsync(ct);

        Assert.Equal(
            (7, "Welcome", true, 2),
            (thread.Id, thread.Subject, thread.IsSticky, thread.MessageCount)
        );

        // Turbo's numbering: 1 up through the forum, 0 up through the thread.
        var posts = await db.GuildForumMessages.OrderBy(x => x.ForumMessageId).ToListAsync(ct);

        Assert.Equal([(1, 0), (2, 1)], posts.Select(x => (x.ForumMessageId, x.ThreadIndex)));
        Assert.Equal((GuildForumState)20, posts[1].State);
        Assert.Equal(10, posts[1].ModeratorEntityId);
    }

    [Fact]
    public async Task Pets_and_bots_stand_where_they_stood_or_wait_in_the_inventory()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = Hotel();
        using var target = new SqliteDb();
        SeedGroups(target);

        var report = await ImportAsync(ms, target);

        await using var db = await target.CreateDbContextAsync(ct);

        var horse = await db.Pets.SingleAsync(x => x.Id == 300, ct);

        Assert.Equal((100, 2, 1), (horse.RoomEntityId, horse.X, horse.Y));
        Assert.Equal(Rotation.South, horse.Rotation);
        Assert.Equal("FFFFFF", horse.Color);
        // 450 experience is past 100, 200 and 400: level 4.
        Assert.Equal(4, horse.Level);
        Assert.Equal(70, horse.Nutrition);
        Assert.True(horse.HasSaddle);
        Assert.True(horse.AnyoneCanRide);
        // Mane and tail as Morningstar drew them, and the saddle's layer.
        Assert.Equal("2 12 5 3 12 5 4 9 0", horse.CustomParts);

        // Its room was not copied: the dog waits in its owner's inventory.
        Assert.Null((await db.Pets.SingleAsync(x => x.Id == 301, ct)).RoomEntityId);

        var bot = await db.Bots.SingleAsync(ct);

        Assert.Equal((400, 100), (bot.Id, bot.RoomEntityId));
        Assert.Equal("Hello\nWelcome!", bot.ChatText);
        Assert.True(bot.AutoChat);
        Assert.Equal(7, bot.ChatDelaySeconds);
        Assert.Equal(AvatarDanceType.PogoMogo, bot.DanceType);
        Assert.True(bot.FreeRoam);
        Assert.Equal(1, report.NotesOf("bot copied as a plain bot: Turbo's bots have no kind"));
        Assert.Equal(1, report.NotesOf("bot left out: its owner was not copied"));
    }
}
