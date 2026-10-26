using System.Runtime.CompilerServices;
using Turbo.Pipeline.Attributes;
using Turbo.Pipeline.Registry;

// The handler contracts are in Turbo.Primitives: a plugin built against this assembly before
// they moved finds them here.
[assembly: TypeForwardedTo(typeof(IHandler<,>))]
[assembly: TypeForwardedTo(typeof(IBehavior<,>))]
[assembly: TypeForwardedTo(typeof(OrderAttribute))]
