using System;
using System.Collections.Generic;

namespace Turbo.Import;

/// <summary>What <c>Turbo.Main import</c> was asked, and what is left over for the host's configuration.</summary>
public sealed record ImportCommandOptions(
    string? Kind,
    string? Source,
    bool DryRun,
    bool Catalog,
    bool Chatlogs,
    IReadOnlyDictionary<int, string> RankGroups,
    bool Help,
    string? Error,
    string[] HostArgs
);

/// <summary>The command line of <c>Turbo.Main import</c>.</summary>
public static class ImportCommandLine
{
    public const string COMMAND = "import";

    public const string MORNINGSTAR = "morningstar";

    public const string USAGE =
        "Usage: Turbo.Main import morningstar --source <connection string> [options]\n"
        + "  Copies an Arcturus Morningstar hotel into this server's database: players, their\n"
        + "  wallets, badges, friends, rooms, furniture, pets, bots and groups. The target must\n"
        + "  hold no players or rooms yet (scripts/sql/fresh-start.sql empties one).\n"
        + "  --source <cs>        the Morningstar database, e.g. \"Server=127.0.0.1;Database=arcturus;User=root;Password=...\"\n"
        + "  --dry-run            read and translate everything, write nothing, and report what would be copied\n"
        + "  --rank <id>=<group>  put Morningstar rank <id> in Turbo permission group <group> (repeatable);\n"
        + "                       unnamed ranks are matched by their name, and rank 1 is the default group\n"
        + "  --catalog            also copy the catalog pages and offers, beside the ones Turbo has\n"
        + "  --chatlogs           also copy room chat logs and private messages already delivered";

    public static bool IsImportCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], COMMAND, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the options after the command. Anything it does not recognise is left in
    /// <see cref="ImportCommandOptions.HostArgs"/>, so a setting such as
    /// <c>--Turbo:Database:ConnectionString=...</c> still reaches the configuration.
    /// </summary>
    public static ImportCommandOptions Parse(string[] args)
    {
        string? kind = null;
        string? source = null;
        var dryRun = false;
        var catalog = false;
        var chatlogs = false;
        var help = false;
        string? error = null;
        var ranks = new Dictionary<int, string>();
        var rest = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--catalog":
                    catalog = true;
                    break;
                case "--chatlogs":
                    chatlogs = true;
                    break;
                case "--source":
                    if (i + 1 < args.Length)
                        source = args[++i];
                    else
                        error ??= "--source needs the Morningstar database's connection string.";

                    break;
                case "--rank":
                    if (
                        i + 1 < args.Length
                        && TryParseRank(args[i + 1], out var rank, out var group)
                    )
                    {
                        ranks[rank] = group;
                        i++;
                    }
                    else
                        error ??= "--rank takes <rank id>=<permission group>, e.g. --rank 7=admin.";

                    break;
                default:
                    if (kind is null && !arg.StartsWith('-'))
                        kind = arg.ToLowerInvariant();
                    else
                        rest.Add(arg);

                    break;
            }
        }

        if (!help)
        {
            if (kind is null)
                error ??=
                    "Say what to import from: import morningstar --source <connection string>.";
            else if (kind != MORNINGSTAR)
                error ??= $"Turbo cannot import from '{kind}'; it knows {MORNINGSTAR}.";
            else if (string.IsNullOrWhiteSpace(source))
                error ??= "--source is required: the Morningstar database's connection string.";
        }

        return new ImportCommandOptions(
            kind,
            source,
            dryRun,
            catalog,
            chatlogs,
            ranks,
            help,
            error,
            [.. rest]
        );
    }

    private static bool TryParseRank(string value, out int rank, out string group)
    {
        rank = 0;
        group = string.Empty;

        var split = value.IndexOf('=', StringComparison.Ordinal);

        if (split <= 0 || split == value.Length - 1)
            return false;

        if (!int.TryParse(value.AsSpan(0, split), out rank))
            return false;

        group = value[(split + 1)..].Trim();

        return group.Length > 0;
    }
}
