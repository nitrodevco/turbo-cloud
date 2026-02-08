using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Plugins;

namespace Turbo.Main.Console;

public class ConsoleCommandService(IServiceProvider services)
{
    private readonly IServiceProvider _services = services;
    private readonly CancellationTokenSource _cts = new();

    private Task? _loopTask;

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void Enable()
    {
        System.Console.WriteLine("Console command service started. Type 'help' for commands.");

        if (IsRunning)
            throw new InvalidOperationException("Already running.");

        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task DisableAsync()
    {
        if (!IsRunning)
            return;

        await _cts.CancelAsync().ConfigureAwait(false);

        if (_loopTask is not null)
#pragma warning disable VSTHRD003
            await _loopTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003

        _cts.Dispose();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var input = await Task.Run(System.Console.ReadLine, ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(input))
                continue;

            await HandleCommandAsync(input, ct).ConfigureAwait(false);
        }
    }

    private async Task HandleCommandAsync(string input, CancellationToken ct)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (cmd)
        {
            case "help":
                System.Console.WriteLine(
                    "Available commands: help, quit, reload-plugins, reload-plugin <key>"
                );
                break;

            case "quit":
            case "exit":
                System.Console.WriteLine("Shutting down...");
                Environment.Exit(0);
                break;

            case "reload-plugins":
                try
                {
                    var pluginMgr = _services.GetRequiredService<PluginManager>();
                    await pluginMgr.LoadAllAsync(true, ct).ConfigureAwait(false);
                    System.Console.WriteLine("Plugins reloaded.");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Reload failed: {ex.Message}");
                }
                break;

            case "reload-plugin":
            {
                if (args.Length == 0)
                {
                    System.Console.WriteLine("Usage: reload-plugin <key>");
                    break;
                }

                try
                {
                    var pluginMgr = _services.GetRequiredService<PluginManager>();
                    await pluginMgr.ReloadAsync(args[0], ct).ConfigureAwait(false);
                    System.Console.WriteLine($"Plugin '{args[0]}' reloaded.");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Reload failed for '{args[0]}': {ex.Message}");
                }
                break;
            }

            default:
                System.Console.WriteLine($"Unknown command: {cmd}");
                break;
        }
    }
}
