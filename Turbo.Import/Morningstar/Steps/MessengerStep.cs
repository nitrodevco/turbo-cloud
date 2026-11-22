using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Messenger;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The friends list: the categories a player sorts friends into (<c>messenger_categories</c>,
/// ids kept), the friendships (<c>messenger_friendships</c>, which like Turbo's holds a row each
/// way, with the relationship heart, smile or skull) and the requests waiting
/// (<c>messenger_friendrequests</c>). A row whose either player was not copied is left out.
/// </summary>
internal sealed class MessengerStep : IImportStep
{
    public string Name => "friends";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var categories = ctx.Batch<MessengerCategoryEntity>("messenger_categories");
        var categoryOwners = new Dictionary<int, int>();
        var categoryNames = new HashSet<(int, string)>();

        await foreach (
            var row in ctx.Source.ReadAsync("messenger_categories", "id", ct).ConfigureAwait(false)
        )
        {
            var id = row.Int("id");
            var player = row.Int("user_id");
            var name = row.Text("name").Trim();

            if (!ctx.Players.Contains(player) || name.Length == 0)
                continue;

            if (!categoryNames.Add((player, name.ToUpperInvariant())))
            {
                ctx.Report.Note(
                    "friend category left out: the player has another of that name",
                    $"category {id} '{name}'"
                );

                continue;
            }

            categoryOwners[id] = player;

            await categories
                .AddAsync(
                    new MessengerCategoryEntity
                    {
                        Id = id,
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        Name = name,
                        PlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await categories.FlushAsync(ct).ConfigureAwait(false);

        var friends = ctx.Batch<MessengerFriendEntity>("messenger_friends");
        var pairs = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("messenger_friendships", "id", ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_one_id");
            var friend = row.Int("user_two_id");

            if (
                player == friend
                || !ctx.Players.Contains(player)
                || !ctx.Players.Contains(friend)
                || !pairs.Add((player, friend))
            )
                continue;

            var category = row.Int("category");

            await friends
                .AddAsync(
                    new MessengerFriendEntity
                    {
                        CreatedAt = row.UnixTime("friends_since") ?? ctx.Now,
                        PlayerEntityId = player,
                        FriendPlayerEntityId = friend,
                        MessengerCategoryEntityId =
                            categoryOwners.TryGetValue(category, out var owner) && owner == player
                                ? category
                                : null,
                        RelationType = Relation(row.Int("relation")),
                        PlayerEntity = null!,
                        FriendPlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await friends.FlushAsync(ct).ConfigureAwait(false);

        var requests = ctx.Batch<MessengerRequestEntity>("messenger_requests");
        var asked = new HashSet<(int, int)>();

        await foreach (
            var row in ctx
                .Source.ReadAsync("messenger_friendrequests", "id", ct)
                .ConfigureAwait(false)
        )
        {
            var from = row.Int("user_from_id");
            var to = row.Int("user_to_id");

            // A request between friends is one Morningstar failed to delete.
            if (
                from == to
                || !ctx.Players.Contains(from)
                || !ctx.Players.Contains(to)
                || pairs.Contains((from, to))
                || !asked.Add((from, to))
            )
                continue;

            await requests
                .AddAsync(
                    new MessengerRequestEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = from,
                        RequestedPlayerEntityId = to,
                        PlayerEntity = null!,
                        RequestedPlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await requests.FlushAsync(ct).ConfigureAwait(false);
    }

    // Morningstar's relation is the client's: 0 none, 1 heart, 2 smile, 3 skull.
    private static MessengerFriendRelationType Relation(int value) =>
        value is >= 0 and <= 3
            ? (MessengerFriendRelationType)Math.Clamp(value, 0, 3)
            : MessengerFriendRelationType.Zero;
}
