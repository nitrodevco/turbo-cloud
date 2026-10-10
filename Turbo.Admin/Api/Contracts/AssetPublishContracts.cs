using System;
using System.Collections.Generic;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A publish target as the panel shows it (<c>AssetTarget</c>): never its password, only whether it
/// has one; <c>pending</c> is how many bundles it lacks or holds an older copy of.
/// </summary>
public sealed record AssetPublishTargetResponse(
    int Id,
    string Name,
    string Protocol,
    string Host,
    int Port,
    string User,
    bool HasPassword,
    string RemotePath,
    string PublicUrl,
    bool AllowSelfSigned,
    string? HostKey,
    int Pending,
    AssetPublishHistoryItem? LastPublish
)
{
    public static AssetPublishTargetResponse From(
        AssetPublishTargetSnapshot target,
        IReadOnlyDictionary<int, string>? names
    ) =>
        new(
            target.Id,
            target.Name,
            AssetPublishProtocols.NameOf(target.Protocol),
            target.Host,
            target.Port,
            target.User,
            target.HasPassword,
            target.RemotePath,
            target.PublicUrl,
            target.AllowSelfSigned,
            target.HostKey,
            target.Pending,
            target.LastPublish is { } last ? AssetPublishHistoryItem.From(last, names) : null
        );
}

/// <summary>The publish targets.</summary>
public sealed record AssetPublishTargetsResponse(AssetPublishTargetResponse[] Items);

/// <summary>
/// A publish target as staff enter it (<c>AssetTargetInput</c>). <c>password</c> null keeps the
/// saved one, and empty removes it.
/// </summary>
public sealed record AssetPublishTargetInput(
    string? Name,
    string? Protocol,
    string? Host,
    int Port,
    string? User,
    string? Password,
    string? RemotePath,
    string? PublicUrl,
    bool AllowSelfSigned
);

/// <summary>Whether a target could be reached and its folder listed, and what happened.</summary>
public sealed record AssetPublishTestResponse(bool Ok, string Message);

/// <summary>A publish to start: a dry run only counts; <c>deleteRemoved</c> also deletes what the hotel no longer has.</summary>
public sealed record AssetPublishRequest(bool DryRun, bool DeleteRemoved);

/// <summary>A publish in a target's history, with the name of who started it when it is known.</summary>
public sealed record AssetPublishHistoryItem(
    int Id,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int PlayerId,
    string? PlayerName,
    bool DryRun,
    int Uploaded,
    int Skipped,
    int Deleted,
    long Bytes,
    string? Error
)
{
    public static AssetPublishHistoryItem From(
        AssetPublishSnapshot publish,
        IReadOnlyDictionary<int, string>? names
    ) =>
        new(
            publish.Id,
            publish.StartedAt,
            publish.FinishedAt,
            publish.PlayerId,
            names?.GetValueOrDefault(publish.PlayerId),
            publish.DryRun,
            publish.Uploaded,
            publish.Skipped,
            publish.Deleted,
            publish.Bytes,
            publish.Error
        );
}

/// <summary>A target's publishes, newest first.</summary>
public sealed record AssetPublishHistoryResponse(AssetPublishHistoryItem[] Items);

/// <summary>
/// The publish job started (<c>AssetJob</c>), its kind and status as the panel names them.
/// </summary>
public sealed record AssetPublishJobResponse(
    Guid Id,
    string Kind,
    string Title,
    string Status,
    string Phase,
    int Total,
    int Done,
    int Failed,
    string[] Log,
    string? Error,
    string? Result,
    int PlayerId,
    DateTime StartedAt,
    DateTime? FinishedAt
)
{
    public static AssetPublishJobResponse From(AssetJobSnapshot job) =>
        new(
            job.Id,
            job.Kind.ToString().ToLowerInvariant(),
            job.Title,
            job.Status.ToString().ToLowerInvariant(),
            job.Phase,
            job.Total,
            job.Done,
            job.Failed,
            [.. job.Log],
            job.Error,
            job.Result,
            job.PlayerId,
            job.StartedAt,
            job.FinishedAt
        );
}

/// <summary>Protocols by the names the panel uses: <c>folder</c>, <c>ftp</c>, <c>ftps</c>, <c>sftp</c>.</summary>
public static class AssetPublishProtocols
{
    public static string NameOf(AssetPublishProtocol protocol) =>
        protocol.ToString().ToLowerInvariant();

    /// <summary>The protocol by its name; false for a name that isn't one (a number included).</summary>
    public static bool TryParse(string? name, out AssetPublishProtocol protocol)
    {
        foreach (var known in Enum.GetValues<AssetPublishProtocol>())
        {
            if (string.Equals(NameOf(known), name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                protocol = known;

                return true;
            }
        }

        protocol = default;

        return false;
    }
}
