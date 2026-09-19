using Orleans;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

public class UpdateSelectorMessageHandler(IGrainFactory grainFactory)
    : UpdateWiredMessageHandler<UpdateSelectorMessage>(grainFactory);
