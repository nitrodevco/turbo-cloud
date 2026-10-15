using System;
using System.Linq;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The words the panel reads for what an upload was found to be and why a sync would take a
/// library, mapped one by one so renaming a member can't change what the panel is sent.
/// </summary>
public static class AssetUploadNames
{
    public static string Of(GamedataFileKind kind) =>
        kind switch
        {
            GamedataFileKind.FurnitureData => "furnidata",
            GamedataFileKind.ProductData => "productdata",
            GamedataFileKind.FigureData => "figuredata",
            GamedataFileKind.FigureMap => "figuremap",
            GamedataFileKind.EffectMap => "effectmap",
            GamedataFileKind.ExternalTexts => "texts",
            GamedataFileKind.ExternalVariables => "variables",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public static string Of(AssetSyncReason reason) =>
        reason switch
        {
            AssetSyncReason.New => "new",
            AssetSyncReason.Updated => "updated",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };
}

/// <summary>
/// What an upload was found to be (<c>AssetUpload</c>): <c>bundle</c>, kept, with the bundle; or
/// <c>gamedata</c>, recognised and not taken, with which file it is.
/// </summary>
public sealed record AssetUploadResponse(string Type, AssetBundleResponse? Bundle, string? Gamedata)
{
    public static AssetUploadResponse From(AssetUploadResult result) =>
        result.Bundle is { } bundle
            ? new("bundle", AssetBundleResponse.From(bundle), null)
            : new("gamedata", null, AssetUploadNames.Of(result.Gamedata!.Value));
}

/// <summary>One of Habbo's libraries a sync would download (<c>AssetSyncPending</c>).</summary>
public sealed record AssetSyncPendingResponse(
    string Name,
    string Reason,
    string HabboRevision,
    string? HotelRevision,
    string? Error
);

/// <summary>One kind's libraries as a sync would find them (<c>AssetSyncKind</c>).</summary>
public sealed record AssetSyncKindResponse(
    string Kind,
    bool Listed,
    int HabboLibraries,
    int UpToDate,
    int Uploads,
    int New,
    int Updated,
    AssetSyncPendingResponse[] Items,
    bool Truncated
);

/// <summary>What a sync from Habbo would download now (<c>AssetSyncPreview</c>).</summary>
public sealed record AssetSyncPreviewResponse(
    string Domain,
    string Revision,
    DateTime ListedAt,
    AssetSyncKindResponse[] Kinds
)
{
    public static AssetSyncPreviewResponse From(AssetSyncPreview preview) =>
        new(
            preview.Domain,
            preview.Revision,
            preview.ListedAt,
            [
                .. preview.Kinds.Select(kind => new AssetSyncKindResponse(
                    AssetContractNames.Of(kind.Kind),
                    kind.Listed,
                    kind.HabboLibraries,
                    kind.UpToDate,
                    kind.Uploads,
                    kind.New,
                    kind.Updated,
                    [
                        .. kind.Items.Select(x => new AssetSyncPendingResponse(
                            x.Name,
                            AssetUploadNames.Of(x.Reason),
                            x.HabboRevision,
                            x.HotelRevision,
                            x.Error
                        )),
                    ],
                    kind.Truncated
                )),
            ]
        );
}
