using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements;

/// <summary>A short identity of the catalog revisions a player's progress is evaluated against.</summary>
internal static class AchievementCatalogStamp
{
    public static string Of(ImmutableArray<AchievementDefinition> catalog)
    {
        var text = string.Join(
            ',',
            catalog
                .OrderBy(x => x.Id)
                .Select(x => $"{x.Id}:{x.Revision}:{(x.Enabled ? 1 : 0)}{(x.Archived ? 1 : 0)}")
        );
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }
}
