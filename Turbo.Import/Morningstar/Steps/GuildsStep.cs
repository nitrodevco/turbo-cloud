using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Guilds;
using Turbo.Import.Source;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// Groups (<c>guilds</c>, ids kept), their members (<c>guilds_members</c>) and their forums
/// (<c>guilds_forums_threads</c>, <c>guilds_forums_comments</c>). A group needs its room and its
/// owner; Turbo has one group a room, so a second group on the same room is left out. A player's
/// favourite group is <c>users_settings.guild_id</c>.
/// <para>
/// Turbo numbers a forum's posts itself (1 up through the forum, 0 up through each thread), so
/// posts are numbered again in Morningstar's order; thread ids are kept.
/// </para>
/// </summary>
internal sealed class GuildsStep : IImportStep
{
    public string Name => "groups";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var forums = await GuildsAsync(ctx, ct).ConfigureAwait(false);

        await MembersAsync(ctx, ct).ConfigureAwait(false);
        await ForumsAsync(ctx, forums, ct).ConfigureAwait(false);
    }

    /// <summary>Writes the groups; answers the forum settings of each group with a forum.</summary>
    private static async Task<Dictionary<int, GuildForumEntity>> GuildsAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var batch = ctx.Batch<GuildEntity>("guilds");
        var forums = new Dictionary<int, GuildForumEntity>();
        var rooms = new HashSet<int>();

        await foreach (var row in ctx.Source.ReadAsync("guilds", "id", ct).ConfigureAwait(false))
        {
            var id = row.Int("id");
            var owner = row.Int("user_id");
            var room = row.Int("room_id");

            if (!ctx.Players.Contains(owner))
            {
                ctx.Report.Note("group left out: its owner was not copied", $"group {id}");

                continue;
            }

            if (!ctx.RoomOwners.ContainsKey(room))
            {
                ctx.Report.Note(
                    "group left out: its room was not copied",
                    $"group {id} room {room}"
                );

                continue;
            }

            if (!rooms.Add(room))
            {
                ctx.Report.Note(
                    "group left out: another group has the same room",
                    $"group {id} room {room}"
                );

                continue;
            }

            ctx.GuildRooms[id] = room;

            var created = row.UnixTime("date_created") ?? ctx.Now;

            await batch
                .AddAsync(
                    new GuildEntity
                    {
                        Id = id,
                        CreatedAt = created,
                        Name = Cut(
                            row.Text("name", $"Group {id}").Trim(),
                            GuildEntity.NAME_MAX_LENGTH
                        ),
                        Description = Cut(
                            row.Text("description").Trim(),
                            GuildEntity.DESCRIPTION_MAX_LENGTH
                        ),
                        BadgeCode = Cut(
                            row.Text("badge").Trim(),
                            GuildEntity.BADGE_CODE_MAX_LENGTH
                        ),
                        PrimaryColorId = row.Int("color_one"),
                        SecondaryColorId = row.Int("color_two"),
                        GuildType = row.Int("state") switch
                        {
                            1 => GuildType.Exclusive,
                            2 or 4 => GuildType.Private,
                            3 => GuildType.Large,
                            _ => GuildType.Regular,
                        },
                        // rights '1': members may decorate the group's room.
                        RightsLevel = row.Bool("rights")
                            ? GuildRightsLevel.Members
                            : GuildRightsLevel.Admins,
                        PlayerEntityId = owner,
                        RoomEntityId = room,
                    },
                    ct
                )
                .ConfigureAwait(false);

            if (row.Bool("forum"))
                forums[id] = new GuildForumEntity
                {
                    CreatedAt = created,
                    GuildEntityId = id,
                    ReadPermission = Permission(row, "read_forum", GuildForumPermission.Everybody),
                    PostMessagePermission = Permission(
                        row,
                        "post_messages",
                        GuildForumPermission.Members
                    ),
                    PostThreadPermission = Permission(
                        row,
                        "post_threads",
                        GuildForumPermission.Members
                    ),
                    ModeratePermission = Permission(row, "mod_forum", GuildForumPermission.Admins),
                };
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);

        return forums;
    }

    private static async Task MembersAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<GuildMemberEntity>("guild_members");
        var seen = new HashSet<(int, int)>();
        var owners = new Dictionary<int, int>();

        await foreach (var row in ctx.Source.ReadAsync("guilds", "id", ct).ConfigureAwait(false))
            if (ctx.GuildRooms.ContainsKey(row.Int("id")))
                owners[row.Int("id")] = row.Int("user_id");

        await foreach (
            var row in ctx.Source.ReadAsync("guilds_members", "id", ct).ConfigureAwait(false)
        )
        {
            var guild = row.Int("guild_id");
            var player = row.Int("user_id");
            var level = row.Int("level_id", 2);

            if (
                level is < 0 or > 3
                || !owners.TryGetValue(guild, out var owner)
                || !ctx.Players.Contains(player)
                || !seen.Add((guild, player))
            )
                continue;

            await batch
                .AddAsync(
                    Member(
                        ctx,
                        guild,
                        player,
                        row.UnixTime("member_since"),
                        (GuildMemberRank)level,
                        owner
                    ),
                    ct
                )
                .ConfigureAwait(false);
        }

        // An owner Morningstar has no member row for is still the group's owner.
        foreach (var (guild, owner) in owners)
            if (seen.Add((guild, owner)))
                await batch
                    .AddAsync(Member(ctx, guild, owner, null, GuildMemberRank.Owner, owner), ct)
                    .ConfigureAwait(false);

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static GuildMemberEntity Member(
        ImportContext ctx,
        int guild,
        int player,
        DateTime? since,
        GuildMemberRank rank,
        int owner
    ) =>
        new()
        {
            CreatedAt = since ?? ctx.Now,
            GuildEntityId = guild,
            PlayerEntityId = player,
            // The owner is the owner whatever level the row says.
            Rank =
                player == owner ? GuildMemberRank.Owner
                : rank == GuildMemberRank.Owner ? GuildMemberRank.Admin
                : rank,
            IsFavourite =
                GuildMemberRanks.IsMember(rank)
                && ctx.UserSettings.TryGetValue(player, out var settings)
                && settings.FavouriteGuild == guild,
        };

    private static async Task ForumsAsync(
        ImportContext ctx,
        Dictionary<int, GuildForumEntity> forums,
        CancellationToken ct
    )
    {
        var threads = new Dictionary<int, GuildForumThreadEntity>();

        await foreach (
            var row in ctx.Source.ReadAsync("guilds_forums_threads", "id", ct).ConfigureAwait(false)
        )
        {
            var id = row.Int("id");
            var guild = row.Int("guild_id");
            var opener = row.Int("opener_id");

            if (!forums.ContainsKey(guild) || !ctx.Players.Contains(opener))
                continue;

            var subject = row.Text("subject").Trim();

            threads[id] = new GuildForumThreadEntity
            {
                Id = id,
                CreatedAt = row.UnixTime("created_at") ?? ctx.Now,
                GuildEntityId = guild,
                PlayerEntityId = opener,
                Subject = Cut(
                    subject.Length == 0 ? "..." : subject,
                    GuildForumThreadEntity.SUBJECT_MAX_LENGTH
                ),
                IsSticky = row.Int("pinned") > 0,
                IsLocked = row.Int("locked") > 0,
                State = State(row.Int("state")),
                ModeratorEntityId = Moderator(ctx, row.Int("admin_id")),
                LastMessageAt = row.UnixTime("updated_at") ?? row.UnixTime("created_at") ?? ctx.Now,
            };
        }

        // Every comment, numbered in its forum and its thread in Morningstar's order.
        var messages = new List<GuildForumMessageEntity>();

        await foreach (
            var row in ctx
                .Source.ReadAsync("guilds_forums_comments", "id", ct)
                .ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");

            if (
                !threads.TryGetValue(row.Int("thread_id"), out var thread)
                || !ctx.Players.Contains(player)
            )
                continue;

            var forum = forums[thread.GuildEntityId];
            var text = row.Text("message");
            var at = row.UnixTime("created_at") ?? thread.CreatedAt;

            forum.MessageCount++;
            forum.LastMessagePlayerEntityId = player;
            forum.LastMessageAt = forum.LastMessageAt is { } last && last > at ? last : at;

            messages.Add(
                new GuildForumMessageEntity
                {
                    CreatedAt = at,
                    GuildEntityId = thread.GuildEntityId,
                    ThreadEntityId = thread.Id,
                    ForumMessageId = forum.MessageCount,
                    ThreadIndex = thread.MessageCount,
                    PlayerEntityId = player,
                    Text = Cut(text, GuildForumMessageEntity.TEXT_MAX_LENGTH),
                    State = State(row.Int("state")),
                    ModeratorEntityId = Moderator(ctx, row.Int("admin_id")),
                }
            );

            thread.MessageCount++;
            thread.LastMessageAt = thread.LastMessageAt > at ? thread.LastMessageAt : at;
        }

        // A thread whose every post was lost (its writers deleted) is not one to show.
        foreach (var empty in threads.Values.Where(x => x.MessageCount == 0).ToList())
            threads.Remove(empty.Id);

        foreach (var thread in threads.Values)
            forums[thread.GuildEntityId].ThreadCount++;

        await ctx.Writer.InsertAsync(forums.Values.ToList(), ct).ConfigureAwait(false);
        ctx.Report.Wrote("guild_forums", forums.Count);

        var threadBatch = ctx.Batch<GuildForumThreadEntity>("guild_forum_threads");

        foreach (var thread in threads.Values.OrderBy(x => x.Id))
            await threadBatch.AddAsync(thread, ct).ConfigureAwait(false);

        await threadBatch.FlushAsync(ct).ConfigureAwait(false);

        var messageBatch = ctx.Batch<GuildForumMessageEntity>("guild_forum_messages");

        foreach (var message in messages.Where(x => threads.ContainsKey(x.ThreadEntityId)))
            await messageBatch.AddAsync(message, ct).ConfigureAwait(false);

        await messageBatch.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Morningstar writes a forum setting by name: EVERYONE, MEMBERS, ADMINS, OWNER.</summary>
    private static GuildForumPermission Permission(
        SourceRow row,
        string column,
        GuildForumPermission fallback
    ) =>
        row.Text(column).Trim().ToUpperInvariant() switch
        {
            "EVERYONE" or "0" => GuildForumPermission.Everybody,
            "MEMBERS" or "1" => GuildForumPermission.Members,
            "ADMINS" or "2" => GuildForumPermission.Admins,
            "OWNER" or "3" => GuildForumPermission.Owner,
            _ => fallback,
        };

    // Both send the client's numbers: 10 and 20 hide a post, anything else shows it.
    private static GuildForumState State(int value) =>
        value switch
        {
            10 => GuildForumState.HiddenByAdmin,
            20 => GuildForumState.HiddenByStaff,
            _ => GuildForumState.Normal,
        };

    private static int? Moderator(ImportContext ctx, int id) =>
        id > 0 && ctx.Players.Contains(id) ? id : null;

    private static string Cut(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
