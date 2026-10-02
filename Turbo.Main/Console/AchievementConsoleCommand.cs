using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Main.Console;

/// <summary>
/// Everything here validates only unless <c>--apply &lt;operation-id&gt; &lt;reason&gt;</c> is given,
/// which publishes one audited batch.
/// </summary>
internal sealed class AchievementConsoleCommand(
    IAchievementCatalog catalog,
    IAchievementPackRegistry packs
)
{
    internal const string USAGE =
        "achievement export|defaults <file>; achievement import <file> [--apply <operation-id> <reason>]; "
        + "achievement retire|unretire|disable|enable|offseason <key> [--apply <operation-id> <reason>]; achievement reload";

    public async Task RunAsync(string[] arguments, CancellationToken ct)
    {
        if (arguments.Length == 1 && arguments[0] == "reload")
        {
            await catalog.ReloadAsync(ct).ConfigureAwait(false);
            System.Console.WriteLine($"Reloaded {catalog.Current.Length} achievements.");

            return;
        }
        if (arguments.Length < 2)
        {
            System.Console.WriteLine(USAGE);

            return;
        }
        var options = new JsonSerializerOptions { WriteIndented = true };
        switch (arguments[0])
        {
            case "export":
            case "defaults":
                ImmutableArray<AchievementDefinition> data =
                    arguments[0] == "defaults"
                        ? [.. packs.Packs.SelectMany(x => x.Definitions)]
                        : catalog.Current;
                await File.WriteAllTextAsync(
                        arguments[1],
                        JsonSerializer.Serialize(data, options),
                        ct
                    )
                    .ConfigureAwait(false);
                System.Console.WriteLine($"Exported {data.Length} definitions.");
                break;
            case "import":
                var (apply, operation, reason) = ParseApply(arguments, "import <file>");
                var definitions = AchievementDefinitionJson.ReadAll(
                    await File.ReadAllTextAsync(arguments[1], ct).ConfigureAwait(false)
                );
                await catalog
                    .ImportAsync(definitions, apply, "console", reason, operation, ct)
                    .ConfigureAwait(false);
                System.Console.WriteLine(
                    apply
                        ? "Catalog published; existing progress and awards retained."
                        : "Catalog valid. Dry run: nothing published."
                );
                break;
            case "retire":
            case "unretire":
            case "disable":
            case "enable":
            case "offseason":
                var (applyState, stateOperation, stateReason) = ParseApply(
                    arguments,
                    arguments[0] + " <key>"
                );
                var change = await catalog
                    .SetStateAsync(
                        arguments[1],
                        StateFor(arguments[0]),
                        applyState,
                        "console",
                        stateReason,
                        stateOperation,
                        ct
                    )
                    .ConfigureAwait(false);
                System.Console.WriteLine(Describe(change, applyState));
                break;
            default:
                System.Console.WriteLine("Unknown achievement catalog operation. " + USAGE);
                break;
        }
    }

    private static AchievementState StateFor(string verb) =>
        verb switch
        {
            "retire" => AchievementState.Archived,
            "disable" => AchievementState.Disabled,
            "offseason" => AchievementState.OffSeason,
            _ => AchievementState.Enabled,
        };

    private static (bool Apply, string Operation, string Reason) ParseApply(
        string[] arguments,
        string usage
    )
    {
        if (arguments.Length != 2 && (arguments.Length < 5 || arguments[2] != "--apply"))
            throw new ArgumentException(
                $"Use {usage} for validation, or {usage} --apply <operation-id> <reason>."
            );
        var apply = arguments.Length >= 5 && arguments[2] == "--apply";

        return (
            apply,
            apply ? arguments[3] : "dry-run",
            apply ? string.Join(" ", arguments[4..]) : "Validate catalog change"
        );
    }

    private static string Describe(AchievementStateChange change, bool applied) =>
        !change.Changed ? $"{change.Key} is already {change.To}; nothing to do."
        : applied
            ? $"{change.Key}: {change.From} -> {change.To} (revision {change.Revision}). Players keep what they earned."
        : $"{change.Key}: {change.From} -> {change.To} would be revision {change.Revision}. Dry run: nothing published.";
}
