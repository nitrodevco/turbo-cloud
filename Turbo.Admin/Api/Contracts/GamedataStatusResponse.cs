using System.Collections.Generic;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The gamedata page's summary: the newest of Habbo's releases, texts and variables and whether
/// each was taken in, the files clients are sent now (the external variables among them) and the
/// variables that carry each file's address, and whether the signed-in staff member may change any
/// of it.
/// </summary>
public sealed record GamedataStatusResponse(
    HabboReleaseSnapshot? LatestRelease,
    HabboTextVersionSnapshot? LatestTexts,
    HabboProductVersionSnapshot? LatestProducts,
    HabboFigureVersionSnapshot? LatestFigures,
    HabboVariableVersionSnapshot? LatestVariables,
    GamedataFileSnapshot FurnitureData,
    GamedataFileSnapshot ExternalTexts,
    GamedataFileSnapshot ProductData,
    GamedataFileSnapshot FigureData,
    GamedataFileSnapshot ExternalVariables,
    GamedataFileSnapshot FigureMap,
    GamedataFileSnapshot EffectMap,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FileKeys,
    bool CanManage
);
