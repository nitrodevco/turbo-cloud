using System;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Primitives.Rooms.Providers;

public interface IWiredDefinitionProvider
{
    public IDisposable RegisterDefinition(
        string wiredType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IWiredDefinition> factory
    );
    public IWiredDefinition CreateWiredInstance(string wiredType, IRoomObjectContext ctx);
}
