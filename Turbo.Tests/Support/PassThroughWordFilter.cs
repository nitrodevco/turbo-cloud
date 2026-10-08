using Turbo.Primitives.Moderation;

namespace Turbo.Tests.Support;

/// <summary>
/// A word filter with no words of the hotel's own, so text passes as typed and only the extra
/// words a caller hands in (a room's list) are replaced. Every fake <see cref="IWordFilter"/> is
/// one: a recording fake would answer a filtered text with null.
/// </summary>
public sealed class PassThroughWordFilter : IWordFilter
{
    public string Replacement => "bobba";

    public string Filter(string text) => text;

    public string Filter(string text, IReadOnlySet<string> extraWords) =>
        FilterWords.Apply(text, extraWords, Replacement);

    public bool IsClean(string text) => true;

    public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
}
