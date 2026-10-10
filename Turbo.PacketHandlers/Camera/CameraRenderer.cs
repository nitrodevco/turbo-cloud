using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// Draws a stored render (<c>photos/&lt;id&gt;.json</c>, <c>thumbnails/&lt;roomId&gt;.json</c>) into the
/// PNG the client loads, by running the renderer command of <see cref="CameraConfig"/>
/// (<c>tools/camera-renderer/render.mjs</c> by default) with the JSON and PNG paths. The client
/// asks for the picture as soon as <c>CameraStorageUrlMessage</c> arrives, so a render is awaited,
/// bounded by <see cref="CameraConfig.RendererTimeoutMilliseconds"/>, waiting included: at most
/// <see cref="CameraConfig.MaxConcurrentRenders"/> run at once.
/// </summary>
public sealed class CameraRenderer(IOptions<CameraConfig> config, ILogger<CameraRenderer> logger)
{
    private readonly CameraConfig _config = config.Value;
    private readonly SemaphoreSlim _slots = new(Math.Max(1, config.Value.MaxConcurrentRenders));

    /// <summary>
    /// True when the PNG exists afterwards; false (logged) when the renderer is off, missing,
    /// failed or timed out. <paramref name="withSmall"/> also writes the half-size
    /// <c>&lt;name&gt;_small.png</c> a photo poster on a wall is drawn from.
    /// </summary>
    public async Task<bool> RenderAsync(
        string jsonPath,
        string pngPath,
        CancellationToken ct,
        bool withSmall = false
    )
    {
        if (string.IsNullOrWhiteSpace(_config.RendererCommand))
            return false;

        var script = Path.GetFullPath(_config.RendererScript, AppContext.BaseDirectory);

        if (!File.Exists(script))
        {
            logger.LogWarning(
                "Camera renderer script {Script} is missing; {Png} not drawn",
                script,
                pngPath
            );

            return false;
        }

        var info = new ProcessStartInfo
        {
            FileName = _config.RendererCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        info.ArgumentList.Add(script);
        info.ArgumentList.Add(Path.GetFullPath(jsonPath));
        info.ArgumentList.Add(Path.GetFullPath(pngPath));
        info.ArgumentList.Add("--cache");
        info.ArgumentList.Add(
            Path.GetFullPath(_config.RendererCacheDirectory, AppContext.BaseDirectory)
        );

        if (!string.IsNullOrWhiteSpace(_config.RendererFurniUrl))
        {
            info.ArgumentList.Add("--furni-url");
            info.ArgumentList.Add(_config.RendererFurniUrl);
        }

        if (withSmall)
            info.ArgumentList.Add("--small");

        foreach (var url in _config.RendererExternalImageUrls)
        {
            info.ArgumentList.Add("--external-url");
            info.ArgumentList.Add(url);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(_config.RendererTimeoutMilliseconds);

        try
        {
            await _slots.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Camera renderer busy for longer than {Timeout} ms; {Png} not drawn",
                _config.RendererTimeoutMilliseconds,
                pngPath
            );

            return false;
        }

        try
        {
            using var process = Process.Start(info);

            if (process is null)
                return false;

            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning(
                    "Camera renderer took longer than {Timeout} ms for {Png}",
                    _config.RendererTimeoutMilliseconds,
                    pngPath
                );

                return false;
            }

            if (process.ExitCode != 0 || !File.Exists(pngPath))
            {
                logger.LogWarning(
                    "Camera renderer exited {Code} for {Png}: {Error}",
                    process.ExitCode,
                    pngPath,
                    (await stderr.ConfigureAwait(false)).Trim()
                );

                return false;
            }

            logger.LogDebug(
                "Camera renderer: {Output}",
                (await stdout.ConfigureAwait(false)).Trim()
            );

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Camera renderer {Command} could not run for {Png}",
                _config.RendererCommand,
                pngPath
            );

            return false;
        }
        finally
        {
            _slots.Release();
        }
    }
}
