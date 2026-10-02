namespace Turbo.Commands;

/// <summary>
/// Command tunables. Every option carries the hotel default it ships with, so a section left out
/// of <c>appsettings.json</c> still starts.
/// </summary>
public class CommandConfig
{
    public const string SECTION_NAME = "Turbo:Commands";

    /// <summary>How long a line waits for its <c>:confirm</c> before it is dropped.</summary>
    public int ConfirmationSeconds { get; init; } = 30;

    /// <summary>The maximum executors retaining a prepared confirmation.</summary>
    public int MaxPendingConfirmations { get; init; } = 1024;

    /// <summary>How often expired prepared commands and their plugin references are released.</summary>
    public int ConfirmationCleanupSeconds { get; init; } = 5;

    /// <summary>Active and queued operator executions across the hotel.</summary>
    public int MaxOutstandingExecutions { get; init; } = 128;

    /// <summary>The maximum active and queued executions for one player or the console.</summary>
    public int MaxOutstandingExecutionsPerExecutor { get; init; } = 8;

    /// <summary>Cooperative deadline; a command that ignores cancellation retains its execution slot.</summary>
    public int ExecutionTimeoutSeconds { get; init; } = 60;

    /// <summary>The maximum player suggestion rate-limit windows retained at once.</summary>
    public int MaxSuggestionWindows { get; init; } = 10000;

    /// <summary>How long an inactive player suggestion window remains cached.</summary>
    public int SuggestionWindowRetentionSeconds { get; init; } = 60;

    /// <summary>
    /// A line that reaches this many players or more waits for <c>:confirm</c>: a selector, a
    /// room or hotel alert. A shutdown and a maintenance always wait, whatever they reach.
    /// </summary>
    public int ConfirmAtPlayers { get; init; } = 10;

    /// <summary>The maximum simultaneous operations across all command batches.</summary>
    public int MaxBatchConcurrency { get; init; } = 4;

    /// <summary>The time allowed for each audit or completion-hook step, independent of request cancellation.</summary>
    public int FinalizationTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// How many suggestion requests (<c>chat.commands</c>) a player may make in a second; one
    /// over it is answered with nothing. Apart from chat flood: a client asks as the player types.
    /// </summary>
    public int SuggestionsPerSecond { get; init; } = 5;

    /// <summary>The most values one suggestion answer holds.</summary>
    public int MaxSuggestions { get; init; } = 20;

    /// <summary>
    /// The shortest prefix a player name is suggested for: a single letter would page through the
    /// hotel's player list.
    /// </summary>
    public int MinPlayerPrefix { get; init; } = 2;
}
