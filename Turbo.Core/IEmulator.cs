namespace Turbo.Core;

using Microsoft.Extensions.Hosting;

public interface IEmulator : IHostedService
{
    public string GetVersion();
}
