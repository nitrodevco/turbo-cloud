using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Admin.Auth;
using Turbo.Admin.Terminal;
using Turbo.Database.Entities.Admin;
using Turbo.Plugins;

namespace Turbo.Main.Console;

public class ConsoleCommandService(IServiceProvider services, IConsoleBroadcaster broadcaster)
    : IConsoleCommandExecutor
{
    private readonly IServiceProvider _services = services;
    private readonly IConsoleBroadcaster _broadcaster = broadcaster;
    private readonly CancellationTokenSource _cts = new();

    private Task? _loopTask;

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void Enable()
    {
        WriteLine("Console command service started. Type 'help' for commands.");

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

    public async Task HandleCommandAsync(string input, CancellationToken ct)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (cmd)
        {
            case "help":
                WriteLine(
                    "Available commands: help, quit, reload-plugins, reload-plugin <key>, create-admin <username> <password> [administrator|moderator]"
                );
                break;

            case "quit":
            case "exit":
                WriteLine("Shutting down...");
                Environment.Exit(0);
                break;

            case "reload-plugins":
                try
                {
                    var pluginMgr = _services.GetRequiredService<PluginManager>();
                    await pluginMgr.LoadAllAsync(true, ct).ConfigureAwait(false);
                    WriteLine("Plugins reloaded.");
                }
                catch (Exception ex)
                {
                    WriteLine($"Reload failed: {ex.Message}");
                }
                break;

            case "reload-plugin":
            {
                if (args.Length == 0)
                {
                    WriteLine("Usage: reload-plugin <key>");
                    break;
                }

                try
                {
                    var pluginMgr = _services.GetRequiredService<PluginManager>();
                    await pluginMgr.ReloadAsync(args[0], ct).ConfigureAwait(false);
                    WriteLine($"Plugin '{args[0]}' reloaded.");
                }
                catch (Exception ex)
                {
                    WriteLine($"Reload failed for '{args[0]}': {ex.Message}");
                }
                break;
            }

            case "create-admin":
            {
                if (args.Length < 2)
                {
                    WriteLine(
                        "Usage: create-admin <username> <password> [administrator|moderator]"
                    );
                    break;
                }

                var role =
                    args.Length >= 3
                    && args[2].Equals("moderator", StringComparison.OrdinalIgnoreCase)
                        ? AdminRoleType.Moderator
                        : AdminRoleType.Administrator;

                try
                {
                    var accountService = _services.GetRequiredService<IAdminAccountService>();
                    var created = await accountService
                        .CreateAdminAsync(args[0], args[1], role, ct)
                        .ConfigureAwait(false);

                    WriteLine(
                        created
                            ? $"Admin user '{args[0]}' created with role {role}."
                            : $"Failed to create admin user '{args[0]}' (username may already exist)."
                    );
                }
                catch (Exception ex)
                {
                    WriteLine($"Failed to create admin user: {ex.Message}");
                }
                break;
            }

            default:
                WriteLine($"Unknown command: {cmd}");
                break;
        }
    }

    private void WriteLine(string line)
    {
        System.Console.WriteLine(line);
        _broadcaster.Publish(line);
    }
}
