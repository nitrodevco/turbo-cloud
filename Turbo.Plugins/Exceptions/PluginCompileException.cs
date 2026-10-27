using System.Collections.Generic;
using System.Linq;

namespace Turbo.Plugins.Exceptions;

/// <summary>
/// A plugin whose sources do not compile: each error as the compiler gives it, with the file
/// (relative to the plugin's folder), line and column, <c>server/SkillBook.cs(42,9): CS0103 ...</c>.
/// </summary>
public sealed class PluginCompileException(string pluginKey, IReadOnlyList<string> errors)
    : PluginException(Describe(errors), pluginKey)
{
    /// <summary>How many errors the message names; <see cref="Errors"/> has them all.</summary>
    internal const int SHOWN = 10;

    public IReadOnlyList<string> Errors { get; } = errors;

    private static string Describe(IReadOnlyList<string> errors) =>
        $"Does not compile: {string.Join("; ", errors.Take(SHOWN))}{(errors.Count > SHOWN ? $" (and {errors.Count - SHOWN} more)" : "")}";
}
