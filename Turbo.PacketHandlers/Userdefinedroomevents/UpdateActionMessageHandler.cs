using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateActionMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateActionMessage>(grainFactory)
{
    // Official client capture 2026-10-24: a 21-pick Chase and Join Team team 0 / 5.
    protected override int ErrorReportMessageId => 3051;
}
