using System;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Server.Abstractions.Session;

namespace Turbo.Networking.Session;

public class SessionContextFactory(IServiceProvider sp) : ISessionFactory
{
    public Type SessionType => typeof(SessionContext);

    public IAppSession Create() => ActivatorUtilities.CreateInstance<SessionContext>(sp);
}
