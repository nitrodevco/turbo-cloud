using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Moderation;

/// <summary>
/// The hotel's word filter (<c>filter_words</c>) as staff keep it in the panel: the words, adding
/// some and taking one off. Each change reloads <see cref="IWordFilter"/>, so chat, messages and
/// names are filtered by the new list at once, and is written to the command log as
/// <c>wordfilter</c>. A word is matched whole, ignoring case, among the letters and digits of what
/// a player types (<see cref="FilterWords"/>), so one with anything else in it would never match
/// and is refused; words are kept in lower case.
/// </summary>
public sealed class AdminWordFilter(
    IDbContextFactory<TurboDbContext> database,
    IWordFilter filter,
    ILogger<AdminWordFilter> logger
)
{
    public const string COMMAND = "wordfilter";
    public const string SOURCE = "panel";

    /// <summary>What separates the words of one entry: spaces, commas, semicolons and new lines.</summary>
    private static readonly char[] SEPARATORS = [' ', ',', ';', '\n', '\r', '\t'];

    public async Task<WordFilterResponse> ListAsync(bool canManage, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var words = await db
            .FilterWords.AsNoTracking()
            .OrderBy(x => x.Word)
            .Select(x => new WordFilterEntry(x.Id, x.Word, x.CreatedAt))
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        return new WordFilterResponse(filter.Replacement, canManage, words);
    }

    /// <summary>
    /// Adds the words in <paramref name="text"/>, split at spaces, commas and new lines; those
    /// already on the list are skipped, and those that could never match are refused, with why.
    /// </summary>
    public async Task<WordFilterAddResponse> AddAsync(
        PlayerId staff,
        string text,
        CancellationToken ct
    )
    {
        var refused = new List<WordFilterRefusal>();
        var wanted = new List<string>();

        foreach (
            var given in (text ?? string.Empty).Split(
                SEPARATORS,
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            var word = given.ToLower(CultureInfo.InvariantCulture);

            if (word.Length > FilterWordEntity.WORD_MAX_LENGTH)
                refused.Add(
                    new WordFilterRefusal(
                        given,
                        $"longer than {FilterWordEntity.WORD_MAX_LENGTH} characters"
                    )
                );
            else if (!word.All(char.IsLetterOrDigit))
                refused.Add(
                    new WordFilterRefusal(
                        given,
                        "only letters and digits are matched: the filter splits what players type at anything else"
                    )
                );
            else if (!wanted.Contains(word))
                wanted.Add(word);
        }

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var existing = await db
            .FilterWords.AsNoTracking()
            .Where(x => wanted.Contains(x.Word))
            .Select(x => x.Word)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = wanted.Where(x => !have.Contains(x)).ToArray();

        if (added.Length > 0)
        {
            db.FilterWords.AddRange(added.Select(x => new FilterWordEntity { Word = x }));
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await ChangedAsync(staff, $"add {string.Join(' ', added)}", ct).ConfigureAwait(false);
        }

        return new WordFilterAddResponse(added, [.. wanted.Where(have.Contains)], [.. refused]);
    }

    /// <summary>Takes a word off the list; false when there is no such word.</summary>
    public async Task<bool> RemoveAsync(PlayerId staff, int id, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var row = await db
            .FilterWords.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        db.FilterWords.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(staff, $"remove {row.Word}", ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>What a player typing <paramref name="text"/> would show, with the list as it is now.</summary>
    public WordFilterTestResponse Test(string text)
    {
        var given = text ?? string.Empty;
        var shown = filter.Filter(given);

        return new WordFilterTestResponse(
            shown,
            !string.Equals(shown, given, StringComparison.Ordinal)
        );
    }

    private async Task ChangedAsync(PlayerId staff, string arguments, CancellationToken ct)
    {
        await filter.ReloadAsync(ct).ConfigureAwait(false);

        try
        {
            var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);

            db.CommandLogs.Add(
                new CommandLogEntity
                {
                    RoomEntityId = 0,
                    PlayerEntityId = staff.Value,
                    Command = COMMAND,
                    Arguments =
                        arguments.Length <= CommandLogEntity.ARGUMENTS_MAX_LENGTH
                            ? arguments
                            : arguments[..CommandLogEntity.ARGUMENTS_MAX_LENGTH],
                    Outcome = CommandTelemetry.Name(CommandOutcome.Completed),
                    ExecutionId = Guid.NewGuid(),
                    Source = SOURCE,
                }
            );

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to write the command log entry for word filter {Arguments} by {Player}",
                arguments,
                staff
            );
        }
    }
}
