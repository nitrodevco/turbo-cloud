using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The words the Assets page reads and sends for the asset enums: <c>furniture</c>, <c>effect</c>,
/// <c>pet</c>, <c>figure</c>; <c>habbo</c>, <c>upload</c>; <c>sync</c>, <c>publish</c>; and so on. Mapped one by one,
/// so renaming a member can't change what the panel is sent.
/// </summary>
public static class AssetContractNames
{
    public static string Of(AssetBundleKind kind) =>
        kind switch
        {
            AssetBundleKind.Furniture => "furniture",
            AssetBundleKind.Effect => "effect",
            AssetBundleKind.Pet => "pet",
            AssetBundleKind.Figure => "figure",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public static string Of(AssetBundleSource source) =>
        source switch
        {
            AssetBundleSource.Habbo => "habbo",
            AssetBundleSource.Upload => "upload",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    public static string Of(AssetJobKind kind) =>
        kind switch
        {
            AssetJobKind.Sync => "sync",
            AssetJobKind.Publish => "publish",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public static string Of(AssetJobStatus status) =>
        status switch
        {
            AssetJobStatus.Running => "running",
            AssetJobStatus.Done => "done",
            AssetJobStatus.Failed => "failed",
            AssetJobStatus.Canceled => "canceled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    public static string Of(AssetCheckSeverity severity) =>
        severity switch
        {
            AssetCheckSeverity.Error => "error",
            AssetCheckSeverity.Warning => "warning",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    public static string Of(AssetBundleStatusFilter status) =>
        status switch
        {
            AssetBundleStatusFilter.All => "all",
            AssetBundleStatusFilter.Ok => "ok",
            AssetBundleStatusFilter.Failed => "failed",
            AssetBundleStatusFilter.Unused => "unused",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    /// <summary>The kind the panel named; false for a word that is not one.</summary>
    public static bool TryParseKind(string? value, out AssetBundleKind kind)
    {
        AssetBundleKind? parsed = value?.Trim().ToLowerInvariant() switch
        {
            "furniture" => AssetBundleKind.Furniture,
            "effect" => AssetBundleKind.Effect,
            "pet" => AssetBundleKind.Pet,
            "figure" => AssetBundleKind.Figure,
            _ => null,
        };

        kind = parsed ?? default;

        return parsed is not null;
    }

    /// <summary>The list's status the panel named, <c>all</c> when it named none; false for a word that is not one.</summary>
    public static bool TryParseStatus(string? value, out AssetBundleStatusFilter status)
    {
        AssetBundleStatusFilter? parsed = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => AssetBundleStatusFilter.All,
            "ok" => AssetBundleStatusFilter.Ok,
            "failed" => AssetBundleStatusFilter.Failed,
            "unused" => AssetBundleStatusFilter.Unused,
            _ => null,
        };

        status = parsed ?? default;

        return parsed is not null;
    }
}

/// <summary>An asset job (<c>AssetJob</c>): a sync or a publish, running or the last one.</summary>
public sealed record AssetJobResponse(
    Guid Id,
    string Kind,
    string Title,
    string Status,
    string Phase,
    int Total,
    int Done,
    int Failed,
    IReadOnlyList<string> Log,
    string? Error,
    string? Result,
    int PlayerId,
    DateTime StartedAt,
    DateTime? FinishedAt
)
{
    public static AssetJobResponse From(AssetJobSnapshot job) =>
        new(
            job.Id,
            AssetContractNames.Of(job.Kind),
            job.Title,
            AssetContractNames.Of(job.Status),
            job.Phase,
            job.Total,
            job.Done,
            job.Failed,
            job.Log,
            job.Error,
            job.Result,
            job.PlayerId,
            job.StartedAt,
            job.FinishedAt
        );
}

/// <summary>A kind's bundles with a file, those that failed, and their size in bytes.</summary>
public sealed record AssetKindResponse(string Kind, int Bundles, int Failed, long Bytes);

/// <summary>Checks that found something, by severity.</summary>
public sealed record AssetCheckCountsResponse(int Errors, int Warnings);

/// <summary>The Assets page at a glance (<c>GET /assets</c>).</summary>
public sealed record AssetOverviewResponse(
    string Directory,
    bool CanManage,
    AssetKindResponse[] Kinds,
    AssetJobResponse? Job,
    AssetCheckCountsResponse Checks,
    int Targets
)
{
    public static AssetOverviewResponse From(
        AssetOverview overview,
        bool canManage,
        AssetJobSnapshot? job
    ) =>
        new(
            overview.Directory,
            canManage,
            [
                .. overview.Kinds.Select(x => new AssetKindResponse(
                    AssetContractNames.Of(x.Kind),
                    x.Bundles,
                    x.Failed,
                    x.Bytes
                )),
            ],
            job is null ? null : AssetJobResponse.From(job),
            new AssetCheckCountsResponse(overview.Errors, overview.Warnings),
            overview.Targets
        );
}

/// <summary>A bundle (<c>AssetBundle</c>).</summary>
public sealed record AssetBundleResponse(
    string Kind,
    string Name,
    string? Revision,
    string Source,
    string? Hash,
    long Size,
    int[] Ids,
    string? Error,
    DateTime UpdatedAt,
    bool Used
)
{
    public static AssetBundleResponse From(AssetBundleSnapshot bundle) =>
        new(
            AssetContractNames.Of(bundle.Kind),
            bundle.Name,
            bundle.Revision,
            AssetContractNames.Of(bundle.Source),
            bundle.Hash,
            bundle.Size,
            [.. bundle.Ids],
            bundle.Error,
            bundle.UpdatedAt,
            bundle.Used
        );
}

/// <summary>A page of bundles.</summary>
public sealed record AssetBundlesResponse(AssetBundleResponse[] Items, int Total, int PageSize)
{
    public static AssetBundlesResponse From(AssetBundlePage page) =>
        new([.. page.Items.Select(AssetBundleResponse.From)], page.Total, page.PageSize);
}

/// <summary>A file in a bundle's zip.</summary>
public sealed record AssetBundleFileResponse(string Name, long Size);

/// <summary>A bundle, where it is kept, and what its zip holds.</summary>
public sealed record AssetBundleDetailResponse(
    string Kind,
    string Name,
    string? Revision,
    string Source,
    string? Hash,
    long Size,
    int[] Ids,
    string? Error,
    DateTime UpdatedAt,
    bool Used,
    string Path,
    AssetBundleFileResponse[] Files
)
{
    public static AssetBundleDetailResponse From(AssetBundleDetail detail)
    {
        var bundle = AssetBundleResponse.From(detail.Bundle);

        return new(
            bundle.Kind,
            bundle.Name,
            bundle.Revision,
            bundle.Source,
            bundle.Hash,
            bundle.Size,
            bundle.Ids,
            bundle.Error,
            bundle.UpdatedAt,
            bundle.Used,
            detail.Path,
            [.. detail.Files.Select(x => new AssetBundleFileResponse(x.Name, x.Size))]
        );
    }
}

/// <summary>A check of the bundles against the hotel.</summary>
public sealed record AssetCheckResponse(
    string Id,
    string Severity,
    string Title,
    string Detail,
    int Count,
    string[] Samples,
    string? Kind,
    string? Status
)
{
    public static AssetCheckResponse From(AssetCheckSnapshot check) =>
        new(
            check.Id,
            AssetContractNames.Of(check.Severity),
            check.Title,
            check.Detail,
            check.Count,
            [.. check.Samples],
            check.Kind is { } kind ? AssetContractNames.Of(kind) : null,
            check.Status is { } status ? AssetContractNames.Of(status) : null
        );
}

public sealed record AssetChecksResponse(AssetCheckResponse[] Items);
