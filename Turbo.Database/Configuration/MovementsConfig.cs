using Turbo.Primitives.Settings;

namespace Turbo.Database.Configuration;

/// <summary>
/// Recording asset movements (furni, pets, bots and currencies changing owner, place or
/// existence) into <c>asset_movement_outbox</c>, for a plugin to consume. See
/// <c>docs/movements.md</c>.
/// </summary>
public class MovementsConfig
{
    public const string SECTION_NAME = "Turbo:Movements";

    /// <summary>
    /// Whether movements are recorded. Off, nothing is written and nothing costs more than
    /// reading this; turning it on or off writes a <c>recording.started</c> or
    /// <c>recording.stopped</c> row, so a consumer can tell "nothing happened" from "nothing was
    /// recorded".
    /// </summary>
    [LiveSetting]
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Whether the server keeps its own currency ledger (<c>currency_ledger</c>) from the outbox
    /// while movements are recorded: every change to a wallet, kept for good, which the admin
    /// panel's Currency log reads. Off, nothing reads the outbox unless a plugin does.
    /// </summary>
    [LiveSetting]
    public bool CurrencyLedger { get; init; } = true;

    /// <summary>
    /// How often, in milliseconds, the dispatcher reads the outbox for its consumers. It reads
    /// only while a plugin has a consumer registered, whatever <see cref="Enabled"/> says.
    /// </summary>
    public int PollMs { get; init; } = 1000;

    /// <summary>
    /// The most rows a consumer is handed in one call, the most pruning deletes in one go (a full
    /// batch is followed by the next at once), and the most skipped-id ranges kept per consumer.
    /// </summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>
    /// How long, in hours, a row is kept, whatever the consumers have read; 0 keeps every row.
    /// A consumer that was behind is told which ids it lost (<c>OutboxGap</c>).
    /// </summary>
    public int OutboxRetentionHours { get; init; } = 72;

    /// <summary>
    /// How old, in milliseconds, a row must be before it is delivered. Ids are given at insert,
    /// not at commit, so a save still committing can hold a lower id than rows already visible;
    /// waiting lets it land in order.
    /// </summary>
    public int SettleMs { get; init; } = 2000;

    /// <summary>
    /// How long, in milliseconds, the ids a consumer's reading skipped are looked for again: a
    /// save that took longer than <see cref="SettleMs"/> to commit is delivered late (rows below
    /// the cursor) instead of never. 0 stops looking.
    /// </summary>
    public int ReReadWindowMs { get; init; } = 300_000;
}
