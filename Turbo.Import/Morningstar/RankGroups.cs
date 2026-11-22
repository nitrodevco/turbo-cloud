using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Import.Morningstar;

/// <summary>
/// Which Turbo permission group a Morningstar rank goes in. Morningstar's ranks are one table of
/// switches (<c>permissions</c>, one column a command); Turbo's are groups of permission nodes
/// (<c>docs/permissions.md</c>). The switches do not carry over one by one: a rank is put in the
/// group that does its job, named by the operator (<c>--rank 7=admin</c>) or guessed from its name.
/// The default group is everyone's without a row, so a rank that lands there writes nothing.
/// </summary>
internal static class RankGroups
{
    public const string DEFAULT = "default";

    // Most specific first: "Senior Moderator" is not a moderator, "Trial Moderator" neither.
    private static readonly (string[] Words, string Group)[] GUESSES =
    [
        (["owner", "founder", "ceo", "admin", "developer", "coder", "hotel manager"], "admin"),
        (["manager", "head"], "manager"),
        (["senior mod", "super mod", "sr mod", "sr. mod"], "senior_moderator"),
        (["trial", "junior mod", "jr mod"], "trial_moderator"),
        (["mod", "staff", "security"], "moderator"),
        (["event"], "events"),
        (["builder", "architect", "designer"], "builder"),
        (["helper", "guide", "x"], "helper"),
        (["ambassador", "amb"], "ambassador"),
        (["vip", "premium", "donator", "donor", "supporter", "club", "legend"], "vip"),
    ];

    /// <summary>
    /// The group a rank goes in, and why, for the log. <paramref name="groups"/> are the groups the
    /// target has, by name.
    /// </summary>
    public static (string Group, string Reason) Resolve(
        int rank,
        string rankName,
        IReadOnlyDictionary<int, string> named,
        IReadOnlySet<string> groups
    )
    {
        if (named.TryGetValue(rank, out var chosen))
            return (chosen, "--rank");

        if (rank <= 1)
            return (DEFAULT, "rank 1 is a player");

        var name = rankName.Trim().ToLowerInvariant();
        var asGroup = name.Replace(' ', '_');

        if (groups.Contains(asGroup))
            return (asGroup, "same name");

        foreach (var (words, group) in GUESSES)
            if (words.Any(word => Matches(name, word)) && groups.Contains(group))
                return (group, $"named '{rankName}'");

        return (DEFAULT, $"nothing matched '{rankName}'");
    }

    private static bool Matches(string name, string word) =>
        word.Length <= 2
            ? name.Split(' ', '-', '_').Contains(word, StringComparer.Ordinal)
            : name.Contains(word, StringComparison.Ordinal);
}
