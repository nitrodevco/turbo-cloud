using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;

namespace Turbo.Main.Console;

/// <summary>Console import defaults to validation only. Explicit --apply publishes one audited batch.</summary>
internal sealed class AchievementConsoleCommand(IAchievementCatalog catalog)
{
    public async Task RunAsync(string[] arguments, CancellationToken ct)
    {
        if (arguments.Length < 2)
        {
            System.Console.WriteLine(
                "achievement export|defaults <file>; achievement import <file> [--apply <operation-id> <reason>]"
            );
            return;
        }
        var options = new JsonSerializerOptions { WriteIndented = true };
        switch (arguments[0])
        {
            case "export":
            case "defaults":
                var data =
                    arguments[0] == "defaults" ? AchievementDefaults.Definitions : catalog.Current;
                await File.WriteAllTextAsync(
                        arguments[1],
                        JsonSerializer.Serialize(data, options),
                        ct
                    )
                    .ConfigureAwait(false);
                System.Console.WriteLine($"Exported {data.Length} definitions.");
                break;
            case "import":
                if (arguments.Length != 2 && (arguments.Length < 5 || arguments[2] != "--apply"))
                    throw new ArgumentException(
                        "Use import <file> for validation, or import <file> --apply <operation-id> <reason>."
                    );
                var definitions = JsonSerializer.Deserialize<ImmutableArray<AchievementDefinition>>(
                    await File.ReadAllTextAsync(arguments[1], ct).ConfigureAwait(false)
                );
                var apply = arguments.Length >= 5 && arguments[2] == "--apply";
                var operation = apply ? arguments[3] : "dry-run";
                var reason = apply ? string.Join(" ", arguments[4..]) : "Validate catalog import";
                await catalog
                    .ImportAsync(definitions, apply, "console", reason, operation, ct)
                    .ConfigureAwait(false);
                System.Console.WriteLine(
                    apply
                        ? "Catalog published; existing progress and awards retained."
                        : "Catalog valid. Dry run: nothing published."
                );
                break;
            default:
                System.Console.WriteLine("Unknown achievement catalog operation.");
                break;
        }
    }
}
