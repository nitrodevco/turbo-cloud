using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Moderation;
using Turbo.Import.Source;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The bans still running (<c>bans</c> whose <c>ban_expire</c> is to come) as account bans in
/// <c>player_sanctions</c>. Turbo bans accounts, so an IP, machine or super ban is copied as a ban
/// of the account it was given to, which the report counts. A ban that ends more than twenty
/// years from now is Morningstar's way of writing a permanent one, and is copied as permanent.
/// Morningstar's escalation history (<c>sanctions</c>) is not carried over.
/// </summary>
internal sealed class SanctionsStep : IImportStep
{
    private static readonly TimeSpan FOREVER = TimeSpan.FromDays(365 * 20);

    public string Name => "bans";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerSanctionEntity>("player_sanctions");
        var now = new DateTimeOffset(ctx.Now).ToUnixTimeSeconds();

        await foreach (var row in ctx.Source.ReadAsync("bans", "id", ct).ConfigureAwait(false))
        {
            var player = row.Int("user_id");
            var expire = row.Long("ban_expire");

            if (!ctx.Players.Contains(player) || expire <= now)
                continue;

            // Far enough ahead to be no date at all (2100 and on) is permanent too.
            var ends = expire - now > FOREVER.TotalSeconds ? null : SourceTime.FromUnix(expire);

            var kind = row.Text("type", "account").Trim().ToLowerInvariant();

            if (kind != "account")
                ctx.Report.Note(
                    "ban copied as an account ban: Turbo bans accounts, not addresses or machines",
                    $"ban {row.Int("id")} ({kind})"
                );

            var issuer = row.Int("user_staff_id");
            var reason = row.Text("ban_reason").Trim();

            await batch
                .AddAsync(
                    new PlayerSanctionEntity
                    {
                        CreatedAt = row.UnixTime("timestamp") ?? ctx.Now,
                        PlayerEntityId = player,
                        Kind = SanctionKind.Ban,
                        Reason =
                            reason.Length > PlayerSanctionEntity.REASON_MAX_LENGTH
                                ? reason[..PlayerSanctionEntity.REASON_MAX_LENGTH]
                                : reason,
                        IssuerEntityId = ctx.Players.Contains(issuer) ? issuer : null,
                        ExpiresAt = ends,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);

        var history = await ctx.Source.CountAsync("sanctions", ct).ConfigureAwait(false);

        if (history > 0)
            ctx.Report.Note(
                "sanction history not copied: Turbo keeps its own",
                $"{history:N0} rows in sanctions"
            );
    }
}
