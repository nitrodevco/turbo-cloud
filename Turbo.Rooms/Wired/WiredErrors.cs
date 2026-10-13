using System.Collections.Immutable;

namespace Turbo.Rooms.Wired;

/// <summary>The rows of the wired monitor's error list, in the order Habbo lists them.</summary>
public enum WiredErrorKind
{
    ExecutionCap,
    DelayedEventsCap,
    ExecutorOverload,
    MarkedAsHeavy,
    Killed,
    RecursionTimeout,
    TooManyVariables,
    TransactionFailure,
    PlacementFailure,
    VariableFxLimit,
}

/// <summary>
/// Habbo's monitor always lists the same ten errors, unused ones with a count of 0 (official
/// client capture of WiredErrorLogs, WIN63-202610121624). The info popup picks its text by the
/// numeric id (<c>wiredmenu.error_info.&lt;id&gt;</c>, WiredErrorInfoView) and the row icon by the
/// category (<c>icon_wired_error_png</c> / <c>icon_wired_warning_png</c>). Placement failure and the
/// variable fx limit share id 8.
/// </summary>
public static class WiredErrors
{
    public const string CATEGORY_ERROR = "ERROR";
    public const string CATEGORY_WARNING = "WARNING";

    public static readonly ImmutableArray<WiredErrorDefinition> All =
    [
        new(0, "EXECUTION_CAP", CATEGORY_ERROR),
        new(1, "DELAYED_EVENTS_CAP", CATEGORY_ERROR),
        new(2, "EXECUTOR_OVERLOAD", CATEGORY_ERROR),
        new(3, "MARKED_AS_HEAVY", CATEGORY_WARNING),
        new(4, "KILLED", CATEGORY_ERROR),
        new(5, "RECURSION_TIMEOUT", CATEGORY_ERROR),
        new(6, "TOO_MANY_VARIABLES", CATEGORY_ERROR),
        new(7, "TRANSACTION_FAILURE", CATEGORY_ERROR),
        new(8, "PLACEMENT_FAILURE", CATEGORY_ERROR),
        new(8, "VARIABLE_FX_LIMIT", CATEGORY_ERROR),
    ];
}

public sealed record WiredErrorDefinition(int Id, string Name, string Category);
