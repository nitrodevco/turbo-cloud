using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateActionMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateActionMessage>(grainFactory);
