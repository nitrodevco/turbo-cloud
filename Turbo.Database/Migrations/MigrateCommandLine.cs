using System;
using System.Collections.Generic;

namespace Turbo.Database.Migrations;

/// <summary>What <c>Turbo.Main migrate</c> was asked, and what is left over for the host's configuration.</summary>
public sealed record MigrateCommandOptions(
    bool Status,
    string? ScriptPath,
    bool AllowDestructive,
    bool Help,
    string? Error,
    string[] HostArgs
);

/// <summary>The command line of <c>Turbo.Main migrate</c>.</summary>
public static class MigrateCommandLine
{
    public const string COMMAND = "migrate";

    public const string USAGE =
        "Usage: Turbo.Main migrate [--status | --script <file>] [--allow-destructive]\n"
        + "  (no option)          apply the pending migrations to the emulator's tables\n"
        + "  --status             say where the database stands; exit 0 up to date, 2 behind, 1 on a problem\n"
        + "  --script <file>      write the SQL for every migration, safe to run twice, and change nothing\n"
        + "  --allow-destructive  let migrations that drop a table or a column run on a database with data";

    public static bool IsMigrateCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], COMMAND, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the options after the command. Anything it does not recognise is left in
    /// <see cref="MigrateCommandOptions.HostArgs"/>, so a setting such as
    /// <c>--Turbo:Database:ConnectionString=...</c> still reaches the configuration.
    /// </summary>
    public static MigrateCommandOptions Parse(string[] args)
    {
        var status = false;
        var allow = false;
        var help = false;
        string? script = null;
        string? error = null;
        var rest = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--status":
                    status = true;
                    break;
                case "--allow-destructive":
                    allow = true;
                    break;
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--script":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        script = args[++i];
                    else
                        error = "--script needs a file to write.";

                    break;
                default:
                    rest.Add(args[i]);
                    break;
            }
        }

        if (status && script is not null)
            error ??= "--status and --script cannot be used together.";

        return new MigrateCommandOptions(status, script, allow, help, error, [.. rest]);
    }
}
