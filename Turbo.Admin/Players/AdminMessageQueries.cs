using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Messenger;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Players;

/// <summary>
/// Players' private messages as staff read them: what friends sent each other in the messenger,
/// newest first, narrowed to one player (what they sent and what was sent to them), a
/// conversation between two, or words in the message. The hotel keeps every message sent to a
/// friend (<c>messenger_messages</c>); group chats are not kept. Paged by id as the chat log is,
/// with no total. Read-only.
/// </summary>
public sealed class AdminMessageQueries(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    private const string ESCAPE = "\\";

    /// <summary>The longest text searched for; the rest is ignored.</summary>
    private const int TEXT_MAX_LENGTH = 255;

    /// <summary>
    /// A run of the log. <paramref name="player"/> and <paramref name="with"/> are names or ids;
    /// both given, the conversation between them. <paramref name="before"/> asks for the messages
    /// older than that one, <paramref name="after"/> for those newer; with neither, the newest.
    /// </summary>
    public async Task<MessageLogResponse> SearchAsync(
        string? player,
        string? with,
        string? text,
        int? before,
        int? after,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.MessagesPageSize);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var messages = db.MessengerMessages.AsNoTracking();
        var nobody = new MessageLogResponse(pageSize, false, false, []);
        int? first = null;
        int? second = null;

        foreach (var (given, slot) in new[] { (player, 0), (with, 1) })
        {
            var who = (given ?? string.Empty).Trim();

            if (who.Length == 0)
                continue;

            if (await PlayerIdAsync(db, who, ct).ConfigureAwait(false) is not { } found)
                return nobody;

            if (slot == 0)
                first = found;
            else
                second = found;
        }

        if (first is { } a && second is { } b)
            messages = messages.Where(x =>
                (x.SenderPlayerEntityId == a && x.ReceiverPlayerEntityId == b)
                || (x.SenderPlayerEntityId == b && x.ReceiverPlayerEntityId == a)
            );
        else if ((first ?? second) is { } one)
            messages = messages.Where(x =>
                x.SenderPlayerEntityId == one || x.ReceiverPlayerEntityId == one
            );

        var words = (text ?? string.Empty).Trim();

        if (words.Length > TEXT_MAX_LENGTH)
            words = words[..TEXT_MAX_LENGTH];

        if (words.Length > 0)
        {
            // LIKE, so case is ignored the same way on every database; the text is escaped so a
            // % or _ in it is matched as itself.
            var like = words
                .Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
                .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", ESCAPE + "_", StringComparison.Ordinal);

            messages = messages.Where(x => EF.Functions.Like(x.Message, "%" + like + "%", ESCAPE));
        }

        List<MessengerMessageEntity> rows;
        bool hasOlder;
        bool hasNewer;

        if (after is { } newerThan)
        {
            rows = await messages
                .Where(x => x.Id > newerThan)
                .OrderBy(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasNewer = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize).Reverse()];
            hasOlder = await messages.AnyAsync(x => x.Id <= newerThan, ct).ConfigureAwait(false);
        }
        else
        {
            var page = before is { } olderThan ? messages.Where(x => x.Id < olderThan) : messages;

            rows = await page.OrderByDescending(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasOlder = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize)];
            hasNewer =
                before is { } newest
                && await messages.AnyAsync(x => x.Id >= newest, ct).ConfigureAwait(false);
        }

        var ids = rows.SelectMany(x => new[] { x.SenderPlayerEntityId, x.ReceiverPlayerEntityId })
            .Distinct()
            .ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return new MessageLogResponse(
            pageSize,
            hasOlder,
            hasNewer,
            [
                .. rows.Select(x => new MessageLogEntry(
                    x.Id,
                    x.Timestamp,
                    x.SenderPlayerEntityId,
                    names.GetValueOrDefault(x.SenderPlayerEntityId),
                    x.ReceiverPlayerEntityId,
                    names.GetValueOrDefault(x.ReceiverPlayerEntityId),
                    x.Message,
                    x.HabbiconId,
                    x.Delivered
                )),
            ]
        );
    }

    /// <summary>A player by id, or by name.</summary>
    private static async Task<int?> PlayerIdAsync(
        TurboDbContext db,
        string who,
        CancellationToken ct
    ) =>
        int.TryParse(who, out var id)
            ? id
            : await db
                .Players.AsNoTracking()
                .Where(x => x.NameKey == PlayerNames.Key(who))
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
}
