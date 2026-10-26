using System.Runtime.CompilerServices;
using Turbo.Events.Registry;

// The contracts an event handler needs are in Turbo.Primitives: a plugin built against this
// assembly before they moved finds them here.
[assembly: TypeForwardedTo(typeof(IEventHandler<>))]
[assembly: TypeForwardedTo(typeof(IEventBehavior<>))]
[assembly: TypeForwardedTo(typeof(EventContext))]
