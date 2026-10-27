using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// A host service a plugin may replace (<c>docs/plugin-services.md</c>): one the plugin registers
/// in its own services is what the whole host calls while the plugin is live (its handlers, the
/// login, the admin API, other plugins), and the host's own comes back when it unloads. The host
/// registers each such contract through its slot (<c>AddReplaceable</c>); every caller reaches the
/// implementation in use through it, so nothing holds on to a replaced one.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class ReplaceableServiceAttribute : Attribute;

/// <summary>
/// The slot of one replaceable host service: the implementation in use, the host's own, and the
/// one under a replacement, for a replacement that adds to what it replaces instead of starting
/// over. A plugin whose replacement is in the slot gets that one under it from the host too
/// (<c>Host&lt;T&gt;()</c>), never its own.
/// </summary>
public interface IServiceSlot<T>
    where T : class
{
    /// <summary>The implementation the host calls now: the last live plugin's that replaced it, else the host's.</summary>
    public T Current { get; }

    /// <summary>The host's own implementation.</summary>
    public T Default { get; }

    /// <summary>The plugin whose implementation is in use; null for the host's.</summary>
    public string? Owner { get; }

    /// <summary>
    /// The implementation <paramref name="replacement"/> replaced: another plugin's, or the host's.
    /// One that is not (or no longer) in the slot gets <see cref="Current"/>.
    /// </summary>
    public T Under(T replacement);
}

/// <summary>
/// The host's replaceable services: the plugin manager puts a plugin's implementation of one in
/// its slot as the plugin starts and takes it out as it stops. Two live plugins replacing one
/// service is logged as a conflict; the one loaded last is used, and the other again once it
/// unloads.
/// </summary>
public interface IReplaceableServices
{
    /// <summary>Whether this host lets a plugin replace <paramref name="contract"/>.</summary>
    public bool IsReplaceable(Type contract);

    /// <summary>
    /// Puts <paramref name="implementation"/> of <paramref name="owner"/> on top of
    /// <paramref name="contract"/>'s slot; disposing the result takes it out wherever it is.
    /// </summary>
    public IDisposable Replace(Type contract, string owner, object implementation);

    /// <summary>Every live replacement, by contract, the one in use last.</summary>
    public ImmutableArray<ServiceReplacement> Replacements { get; }
}

/// <summary>
/// One plugin's replacement of a host service: the contract, the plugin, its class, and whether
/// it is the one in use (false: another plugin loaded later replaced it too).
/// </summary>
public sealed record ServiceReplacement(
    string Contract,
    string Owner,
    string Implementation,
    bool InUse
);
