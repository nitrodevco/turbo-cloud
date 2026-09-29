using System.Reflection;
using System.Runtime.CompilerServices;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;

namespace EvalHarness;

public static class SessionAsserts
{
    /// <summary>The observer the presence grain currently routes to, found by type not name.</summary>
    public static object? PresenceObserver(object presence) =>
        presence
            .GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(ISessionContextObserver))
            .Select(f => f.GetValue(presence))
            .FirstOrDefault();

    /// <summary>Any composer instance, without running constructors or required-member checks.</summary>
    public static IComposer AnyComposer<T>()
        where T : IComposer => (IComposer)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
