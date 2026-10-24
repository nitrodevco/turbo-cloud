using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateVariableMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateVariableMessage>(grainFactory)
{
    // Official client capture 2026-10-20: a refused variable save.
    protected override int ErrorReportMessageId => 3059;
}
