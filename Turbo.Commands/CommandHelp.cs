using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Texts;

namespace Turbo.Commands;

/// <summary>One help renderer for room notices and the console, using the execution schema.</summary>
public static class CommandHelp
{
    public static IReadOnlyList<string> Describe(
        ICommandRegistry registry,
        CommandDescriptor command,
        Func<string, bool> hasPermission,
        IHotelTextProvider? texts = null,
        bool details = false
    )
    {
        var lines = new List<string>();
        var description =
            texts is not null
            && texts.TryGetText(
                CommandReplyKeys.ForCommand(command.Name, CommandReplyKeys.DESCRIPTION),
                out var localized
            )
                ? localized
                : command.Description;
        var binders =
            command.Binder.Syntax.Count == 0
                ? [command.Binder]
                : command
                    .Binder.Syntax.Where(x => x.Permission is null || hasPermission(x.Permission))
                    .Select(x => x.Binder)
                    .ToArray();
        foreach (var binder in binders)
        {
            var line =
                binder.Usage + (description.Length == 0 ? string.Empty : " - " + description);
            if (binder.SelectorNode is { } node && hasPermission(node))
                line +=
                    $" (<{binder.SelectorParameter}> may be {PlayerTarget.ROOM} or {PlayerTarget.ONLINE})";
            lines.Add(line);
            if (!details)
                continue;
            foreach (var parameter in binder.Parameters)
            {
                var parts = new List<string>
                {
                    parameter.Name + ": " + parameter.Kind.ToString().ToLowerInvariant(),
                };
                if (parameter.Description.Length > 0)
                    parts.Add(parameter.Description);
                if (parameter.Members.Length > 0)
                    parts.Add(string.Join(" | ", parameter.Members));
                if (parameter.Minimum is not null || parameter.Maximum is not null)
                    parts.Add($"range {parameter.Minimum}..{parameter.Maximum}");
                if (parameter.MinLength >= 0 || parameter.MaxLength >= 0)
                    parts.Add($"length {parameter.MinLength}..{parameter.MaxLength}");
                if (parameter.Optional)
                    parts.Add(
                        parameter.DefaultValue.Length == 0
                            ? "optional"
                            : "default: " + parameter.DefaultValue
                    );
                lines.Add("  " + string.Join("; ", parts));
            }
        }
        if (details && registry.AliasesOf(command) is { Count: > 0 } aliases)
            lines.Add("Aliases: " + string.Join(", ", aliases));
        return lines;
    }
}
