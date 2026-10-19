using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Primitives.Movements;

namespace Turbo.Operations.Movements;

/// <summary>
/// Keeps the <see cref="CurrencyLedger"/> registered while movements are recorded
/// (<see cref="MovementsConfig.Enabled"/>, a live setting) and the ledger is wanted
/// (<see cref="MovementsConfig.CurrencyLedger"/>): turned on, it reads on from its cursor;
/// turned off, the outbox is not read for it, as a hotel without consumers never reads it.
/// </summary>
public sealed class CurrencyLedgerService(
    CurrencyLedger ledger,
    IAssetMovementConsumerRegistry registry,
    IOptionsMonitor<MovementsConfig> config
) : IHostedService, IDisposable
{
    private readonly Lock _gate = new();
    private IDisposable? _registration;
    private IDisposable? _watch;

    /// <summary>Whether the ledger is registered now.</summary>
    public bool IsReading
    {
        get
        {
            lock (_gate)
                return _registration is not null;
        }
    }

    public Task StartAsync(CancellationToken ct)
    {
        _watch = config.OnChange(Apply);
        Apply(config.CurrentValue);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Dispose();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _watch?.Dispose();
        _watch = null;

        lock (_gate)
        {
            _registration?.Dispose();
            _registration = null;
        }
    }

    private void Apply(MovementsConfig current)
    {
        lock (_gate)
        {
            var wanted = current.Enabled && current.CurrencyLedger;

            if (wanted && _registration is null)
                _registration = registry.Register(ledger);
            else if (!wanted && _registration is not null)
            {
                _registration.Dispose();
                _registration = null;
            }
        }
    }
}
