using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The gamedata page's summary: the newest of Habbo's releases and texts and whether each was taken
/// in, the files clients are sent now (the external variables among them), and whether the
/// signed-in staff member may change any of it.
/// </summary>
public sealed record GamedataStatusResponse(
    HabboReleaseSnapshot? LatestRelease,
    HabboTextVersionSnapshot? LatestTexts,
    HabboProductVersionSnapshot? LatestProducts,
    HabboFigureVersionSnapshot? LatestFigures,
    GamedataFileSnapshot FurnitureData,
    GamedataFileSnapshot ExternalTexts,
    GamedataFileSnapshot ProductData,
    GamedataFileSnapshot FigureData,
    GamedataFileSnapshot ExternalVariables,
    bool CanManage
);
