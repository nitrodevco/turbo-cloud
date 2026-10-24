using System.Reflection;
using FluentAssertions;
using Turbo.Rooms.Configuration;
using Xunit;

namespace Turbo.Tests.Architecture;

/// <summary>
/// No setting of the wired and room configs without a line in <c>config-allowlist.txt</c> saying
/// why a hotel may change it (policy, limit, data, client, engine; legacy for the ones kept from
/// before the list). Turbo ships Habbo's behaviour as the only behaviour: a setting that brings
/// back an older one, or one per timing or detail, is not added (30 such were removed in #363).
/// </summary>
public sealed class ConfigAllowlistTests
{
    private static readonly Type[] CONFIGS =
    [
        typeof(WiredConfig),
        typeof(WiredChestConfig),
        typeof(RoomConfig),
    ];

    private static readonly HashSet<string> KINDS =
    [
        "policy",
        "limit",
        "data",
        "client",
        "engine",
        "legacy",
    ];

    [Fact]
    public void Every_setting_is_on_the_allowlist_with_its_kind()
    {
        var listed = Allowlist();
        var settings = CONFIGS
            .SelectMany(type =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.SetMethod?.IsPublic == true)
                    .Select(p => $"{type.Name}.{p.Name}")
            )
            .ToHashSet(StringComparer.Ordinal);

        var offenders = settings
            .Where(s => !listed.ContainsKey(s))
            .Select(s => $"{s} is not on the allowlist")
            .Concat(
                listed
                    .Where(x => !settings.Contains(x.Key))
                    .Select(x => $"{x.Key} is listed but is no setting")
            )
            .Concat(
                listed
                    .Where(x => !KINDS.Contains(x.Value))
                    .Select(x => $"{x.Key} is listed as {x.Value}, not a kind")
            );

        offenders.Should().BeEmpty();
    }

    private static Dictionary<string, string> Allowlist()
    {
        using var stream = typeof(ConfigAllowlistTests).Assembly.GetManifestResourceStream(
            "Architecture.config-allowlist.txt"
        )!;
        using var reader = new StreamReader(stream);
        var listed = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = string.Empty;

        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('['))
            {
                section = line.Trim('[', ']');

                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);

            listed[$"{section}.{parts[0]}"] = parts.Length == 2 ? parts[1] : string.Empty;
        }

        return listed;
    }
}
