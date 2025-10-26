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
            await _loopTask.ConfigureAwait(false);

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

    private Task HandleCommandAsync(string input, CancellationToken ct)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (cmd)
        {
            case "help":
                System.Console.WriteLine("Available commands: help, quit, reload-plugins");
                break;

            case "quit":
            case "exit":
                System.Console.WriteLine("Shutting down...");
                Environment.Exit(0);
                break;

            case "reload-plugins":
                var pluginMgr = _services.GetRequiredService<PluginManager>();
                //await pluginMgr.LoadAll(true, false, ct);
                break;

            case "reload-plugin":
            {
                pluginMgr = _services.GetRequiredService<PluginManager>();
                //await pluginMgr.Reload(args[0], ct);
                break;
            }

            default:
                System.Console.WriteLine("Unknown command: {Command}", cmd);
                break;
        }

        return Task.CompletedTask;
    }
}
