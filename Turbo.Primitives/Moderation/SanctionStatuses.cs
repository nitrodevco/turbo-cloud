using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// The sanctions a signed-in player can be under, for the help window's sanction info. A ban
/// cannot be among them (a banned player is not let in); a silence and a trade lock are
/// denials of <see cref="PermissionNodes.Chat.SPEAK"/> and <see cref="PermissionNodes.TRADE"/>,
/// so each is listed while an assignment denies its node. The client shows the description as
/// it is, so it is a hotel text: <c>%0%</c> is when the sanction ends.
/// </summary>
public static class SanctionStatuses
{
    public const string MUTE = "MUTE";
    public const string TRADE_LOCK = "TRADE_LOCK";

    public const string MUTE_TEXT = "moderation.sanction.mute";
    public const string MUTE_TEXT_PERMANENT = "moderation.sanction.mute.permanent";
    public const string TRADE_LOCK_TEXT = "moderation.sanction.trade_lock";
    public const string TRADE_LOCK_TEXT_PERMANENT = "moderation.sanction.trade_lock.permanent";

    private const string DEFAULT_MUTE_TEXT = "You can't speak in the hotel until %0%.";
    private const string DEFAULT_MUTE_TEXT_PERMANENT = "You can't speak in the hotel.";
    private const string DEFAULT_TRADE_LOCK_TEXT = "You can't trade until %0%.";
    private const string DEFAULT_TRADE_LOCK_TEXT_PERMANENT = "You can't trade.";

    /// <summary>The nodes whose denial is a sanction, and the name each is shown under.</summary>
    public static readonly ImmutableArray<(string Node, string Name)> NODES =
    [
        (PermissionNodes.Chat.SPEAK, MUTE),
        (PermissionNodes.TRADE, TRADE_LOCK),
    ];

    /// <summary>
    /// The sanctions in force, from why the player does or does not hold each of
    /// <see cref="NODES"/>, in that order. Only a denial counts: a node nobody granted is not a
    /// sanction.
    /// </summary>
    public static async Task<ImmutableArray<SanctionStatusSnapshot>> FromChecksAsync(
        IReadOnlyList<PermissionCheckSnapshot> checks,
        IHotelTextProvider texts,
        DateTime nowUtc,
        CancellationToken ct
    )
    {
        var sanctions = ImmutableArray.CreateBuilder<SanctionStatusSnapshot>();

        foreach (var check in checks)
        {
            if (check.Granted || check.Decision is not { Value: false } decision)
                continue;

            var name = NameOf(check.Node);

            if (name is null)
                continue;

            var endsAt = decision.GrantedUntil ?? decision.ExpiresAt;

            sanctions.Add(
                new SanctionStatusSnapshot
                {
                    Type = new SanctionTypeSnapshot
                    {
                        Name = name,
                        // Hours still to run, rounded up; 0 for a sanction without an end.
                        LengthHours = endsAt is { } end
                            ? (int)Math.Ceiling(Math.Max(0, (end - nowUtc).TotalHours))
                            : 0,
                        Unknown = 0,
                    },
                    Description = await DescriptionAsync(name, endsAt, texts, ct)
                        .ConfigureAwait(false),
                    Gradual = false,
                    ProbationHoursLeft = 0,
                    NextType = SanctionTypeSnapshot.NONE,
                }
            );
        }

        return sanctions.ToImmutable();
    }

    private static string? NameOf(string node)
    {
        foreach (var (candidate, name) in NODES)
            if (string.Equals(candidate, node, StringComparison.Ordinal))
                return name;

        return null;
    }

    private static async Task<string> DescriptionAsync(
        string name,
        DateTime? endsAt,
        IHotelTextProvider texts,
        CancellationToken ct
    )
    {
        var mute = name == MUTE;
        var permanent = endsAt is null;
        var (key, fallback) = (mute, permanent) switch
        {
            (true, true) => (MUTE_TEXT_PERMANENT, DEFAULT_MUTE_TEXT_PERMANENT),
            (true, false) => (MUTE_TEXT, DEFAULT_MUTE_TEXT),
            (false, true) => (TRADE_LOCK_TEXT_PERMANENT, DEFAULT_TRADE_LOCK_TEXT_PERMANENT),
            (false, false) => (TRADE_LOCK_TEXT, DEFAULT_TRADE_LOCK_TEXT),
        };
        var text = await texts.GetTextAsync(key, ct).ConfigureAwait(false) ?? fallback;

        return text.Replace(
            "%0%",
            endsAt?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                ?? string.Empty,
            StringComparison.Ordinal
        );
    }
}
