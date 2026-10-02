using System.Collections.Frozen;
using System.Collections.Generic;

namespace Turbo.Primitives.Commands;

internal static class NoTexts
{
    public static IReadOnlyDictionary<string, string> Instance { get; } =
        new Dictionary<string, string>().ToFrozenDictionary();
}
