using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api;
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
/// friend (<c>messenger_messages</c>); group chats are not kept. Counted and sorted as the chat log
/// is. Read-only.
/// </summary>
public sealed class AdminMessageQueries(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    private const string ESCAPE = "\\";

    /// <summary>The longest text searched for; the rest is ignored.</summary>
    private const int TEXT_MAX_LENGTH = 255;

    /// <summary>The columns the log sorts by: when, the message's id, which its key orders already.</summary>
    private static readonly ListSorts<MessengerMessageEntity> SORTS =
        new ListSorts<MessengerMessageEntity>().Add("when", ListOrder.DESC, x => x.Id);

    /// <summary>
    /// A page of the log, counted up to <see cref="LogCounts.LIMIT"/>. <paramref name="player"/>
    /// and <paramref name="with"/> are names or ids; both given, the conversation between them.
    /// </summary>
    public async Task<MessageLogResponse> SearchAsync(
        string? player,
        string? with,
        string? text,
        int page,
        string? sort,
        string? dir,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.MessagesPageSize);
        var order = SORTS.Resolve(sort, dir);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var messages = db.MessengerMessages.AsNoTracking();
        var nobody = new MessageLogResponse(0, false, 1, pageSize, order, []);
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

        var sorted = SORTS.Apply(messages, order);
        var (total, capped) = await LogCounts.CountAsync(sorted, ct).ConfigureAwait(false);

        page = LogCounts.Page(page, total, pageSize);

        var rows = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ids = rows.SelectMany(x => new[] { x.SenderPlayerEntityId, x.ReceiverPlayerEntityId })
            .Distinct()
            .ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return new MessageLogResponse(
            total,
            capped,
            page,
            pageSize,
            order,
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
