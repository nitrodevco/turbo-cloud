using System;
using System.Collections.Generic;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata;

namespace Turbo.Gamedata.Assets;

/// <summary>Habbo's libraries by kind, as read at <see cref="ListedAt"/>; null where a map could not be read.</summary>
internal sealed record AssetDiscoveryResult(
    string Domain,
    string Revision,
    DateTime ListedAt,
    List<HabboLibrary> Furniture,
    List<HabboLibrary>? Figures,
    List<HabboLibrary>? Effects,
    List<HabboLibrary> Pets,
    List<HabboLibrary> Room,
    List<HabboLibrary>? Badges,
    List<HabboLibrary> CatalogIcons,
    List<HabboLibrary> CatalogImages,
    List<HabboLibrary> Sounds
)
{
    /// <summary>The release of Habbo's HTML client the room libraries were listed from, when one is downloaded.</summary>
    public HabboClientRelease? Client { get; init; }
}
