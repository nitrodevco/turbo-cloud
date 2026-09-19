using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Bots;

/// <summary>A bot's chat text is one line per row of the chatter editor.</summary>
public static class BotChatLines
{
    public static ImmutableArray<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var line in text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0)
                builder.Add(trimmed);
        }

        return builder.ToImmutable();
    }
}
