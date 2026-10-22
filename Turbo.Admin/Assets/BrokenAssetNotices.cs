using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Events.Registry;
using Turbo.Primitives.Plugins.Events;

namespace Turbo.Admin.Assets;

/// <summary>
/// Hears the client errors plugin's notices of new and returning issues (turbo-cloud's
/// <c>docs/plugin-notices.md</c>) and keeps each address a client was refused
/// (<c>Invalid response 404 for &lt;url&gt;</c>, nitro's asset manager) for the broken assets page.
/// The publisher waits for handlers, so the notice is only queued here.
/// </summary>
public sealed partial class BrokenAssetNoticeHandler(BrokenAssetReportQueue queue)
    : IEventHandler<PluginNoticeEvent>
{
    public const string ISSUE_NEW = "issue.new";
    public const string ISSUE_REOPENED = "issue.reopened";

    public ValueTask HandleAsync(PluginNoticeEvent notice, EventContext ctx, CancellationToken ct)
    {
        if (ReportOf(notice) is { } report)
            queue.Add(report);

        return ValueTask.CompletedTask;
    }

    /// <summary>The address a client error notice says was refused; null for any other notice.</summary>
    public static BrokenAssetReport? ReportOf(PluginNoticeEvent notice)
    {
        if (
            notice.Source != AdminBrokenAssets.CLIENT_ERRORS_PLUGIN
            || notice.Topic is not (ISSUE_NEW or ISSUE_REOPENED)
            || notice.Text is not { Length: > 0 } text
        )
            return null;

        var match = RefusedPattern().Match(text);

        if (
            !match.Success
            || !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var status)
            || status is < 400 or > 499
            || !Uri.TryCreate(match.Groups[2].Value, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https")
        )
            return null;

        return new BrokenAssetReport(match.Groups[2].Value, status, notice.Url);
    }

    [GeneratedRegex(@"Invalid response (\d{3}) for (\S+)")]
    private static partial Regex RefusedPattern();
}

/// <summary>
/// The refused addresses heard and not yet kept, saved one at a time in the background. Full
/// (a flood of new issues), a report is dropped: the next issue of the address tells of it again.
/// </summary>
public sealed class BrokenAssetReportQueue(
    AdminBrokenAssets brokenAssets,
    ILogger<BrokenAssetReportQueue> logger
) : BackgroundService
{
    private const int CAPACITY = 1000;

    private readonly Channel<BrokenAssetReport> _reports = Channel.CreateBounded<BrokenAssetReport>(
        new BoundedChannelOptions(CAPACITY)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        }
    );

    public void Add(BrokenAssetReport report) => _reports.Writer.TryWrite(report);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (
            var report in _reports.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)
        )
        {
            try
            {
                await brokenAssets.RecordAsync(report, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "The refused address {Url} could not be kept", report.Url);
            }
        }
    }
}
