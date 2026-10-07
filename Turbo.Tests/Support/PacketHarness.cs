using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909;

namespace Turbo.Tests.Support;

/// <summary>
/// Drives a packet the way the server would: client bytes, the revision's parser, the handler
/// registered for the parsed message, the composers it sends, and the revision's serializers.
/// A test states only wire bytes in and wire bytes out, so it does not depend on the names an
/// implementation gives its message fields, composer properties or helpers.
/// </summary>
public sealed class PacketHarness
{
    public static readonly Revision20260909 Revision = new();

    public Fakes Fakes { get; } = new();
    public Resolver Resolver { get; }
    public ConcurrentQueue<IComposer> Sent { get; } = new();

    public PacketHarness()
    {
        Resolver = new Resolver(Fakes);
        Fakes.Handlers["SendComposerAsync"] = call =>
        {
            foreach (var a in call.Args)
            {
                if (a is IComposer c)
                    Sent.Enqueue(c);
                else if (a is IEnumerable<IComposer> many)
                    foreach (var m in many)
                        Sent.Enqueue(m);
            }
            return Fakes.NotHandled;
        };
    }

    /// <summary>An incoming header id by its constant name in the revision's Headers.cs.</summary>
    public static int Incoming(string name) => Header("MessageEvent", name);

    /// <summary>An outgoing header id by its constant name in the revision's Headers.cs.</summary>
    public static int Outgoing(string name) => Header("MessageComposer", name);

    private static int Header(string cls, string name) =>
        (int)
            typeof(Revision20260909)
                .Assembly.GetType($"Turbo.Revisions.Revision20260909.{cls}")!
                .GetField(name)!
                .GetRawConstantValue()!;

    public static byte[] Payload(Action<PayloadWriter> write)
    {
        var w = new PayloadWriter();
        write(w);
        return w.ToArray();
    }

    /// <summary>Parses the payload with the parser registered for <paramref name="header"/>.</summary>
    public static IMessageEvent Parse(int header, byte[] payload)
    {
        var parser = Revision.Parsers[header];
        return parser.Parse(new ClientPacket(header, payload));
    }

    /// <summary>Runs the handler for the message and returns what it sent to the session.</summary>
    public async Task<List<ClientPacket>> SendAsync(
        int header,
        byte[] payload,
        int playerId = 1,
        int roomId = -1
    )
    {
        var message = Parse(header, payload);
        var handlerType = FindHandler(message.GetType());
        var handler = Resolver.Resolve(handlerType)!;
        var session = Fakes.Create<ISessionContext>("session");
        var ctx = CreateContext(session, playerId, roomId);

        var method = handlerType.GetMethod("HandleAsync")!;
        var result = method.Invoke(handler, [message, ctx, CancellationToken.None]);
        if (result is ValueTask vt)
            await vt;
        else if (result is Task t)
            await t;

        return Sent.Select(Encode).ToList();
    }

    public static ClientPacket Encode(IComposer composer)
    {
        var serializer = Revision.Serializers[composer.GetType()];
        var packet = serializer.Serialize(composer);
        try
        {
            var framed = packet.ToArray();
            // Frame: int32 length (of what follows), int16 header, payload.
            var length = BinaryPrimitives.ReadInt32BigEndian(framed.AsSpan(0, 4));
            var header = BinaryPrimitives.ReadInt16BigEndian(framed.AsSpan(4, 2));
            if (length != framed.Length - 4 || header != serializer.Header)
                throw new InvalidOperationException(
                    $"Malformed frame for {composer.GetType().Name}: length {length}, header {header}"
                );
            return new ClientPacket(header, framed.AsMemory(6).ToArray());
        }
        finally
        {
            // Older revisions of IServerPacket are not disposable.
            (packet as IDisposable)?.Dispose();
        }
    }

    private static MessageContext CreateContext(ISessionContext session, int playerId, int roomId)
    {
        foreach (var ctor in typeof(MessageContext).GetConstructors())
        {
            var ps = ctor.GetParameters();
            var args = new object?[ps.Length];
            for (var i = 0; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;
                if (pt == typeof(ISessionContext))
                    args[i] = session;
                else if (ps[i].Name!.Contains("player", StringComparison.OrdinalIgnoreCase))
                    args[i] = Convert(pt, playerId);
                else if (ps[i].Name!.Contains("room", StringComparison.OrdinalIgnoreCase))
                    args[i] = Convert(pt, roomId);
                else
                    args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            return (MessageContext)ctor.Invoke(args);
        }
        throw new InvalidOperationException("No MessageContext constructor");
    }

    private static object Convert(Type t, int value)
    {
        if (t == typeof(int))
            return value;
        var implicitOp = t.GetMethod("op_Implicit", [typeof(int)]);
        if (implicitOp is not null)
            return implicitOp.Invoke(null, [value])!;
        return Activator.CreateInstance(t, value)!;
    }

    private static Type FindHandler(Type messageType)
    {
        var asm = typeof(Turbo.PacketHandlers.Catalog.GetCatalogIndexMessageHandler).Assembly;
        var iface = typeof(IMessageHandler<>).MakeGenericType(messageType);
        return asm.GetTypes().Single(t => !t.IsAbstract && iface.IsAssignableFrom(t));
    }
}

public sealed class PayloadWriter
{
    private readonly List<byte> _bytes = [];

    public PayloadWriter String(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        var len = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(len, (short)b.Length);
        _bytes.AddRange(len);
        _bytes.AddRange(b);
        return this;
    }

    public PayloadWriter Int(int i)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, i);
        _bytes.AddRange(b);
        return this;
    }

    public PayloadWriter Short(short s)
    {
        var b = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(b, s);
        _bytes.AddRange(b);
        return this;
    }

    public PayloadWriter Byte(byte v)
    {
        _bytes.Add(v);
        return this;
    }

    public PayloadWriter Bool(bool v)
    {
        _bytes.Add(v ? (byte)1 : (byte)0);
        return this;
    }

    public byte[] ToArray() => [.. _bytes];
}

/// <summary>
/// A small constructor resolver: real Turbo implementations where one can be built from
/// defaults, the shipped config defaults for options, null loggers, and recording fakes for
/// anything else (grains, databases).
/// </summary>
public sealed class Resolver(Fakes fakes)
{
    private static readonly Assembly[] TurboAssemblies = AppDomain
        .CurrentDomain.GetAssemblies()
        .Concat(LoadTurbo())
        .Where(a => a.GetName().Name?.StartsWith("Turbo.") == true)
        .Distinct()
        .ToArray();

    private static IEnumerable<Assembly> LoadTurbo()
    {
        var dir = AppContext.BaseDirectory;
        foreach (var f in Directory.GetFiles(dir, "Turbo.*.dll"))
        {
            Assembly? a = null;
            try
            {
                a = Assembly.LoadFrom(f);
            }
            catch (Exception)
            {
                // not loadable here; skip
            }
            if (a is not null)
                yield return a;
        }
    }

    public Dictionary<Type, object> Overrides { get; } = [];

    public object? Resolve(Type t, int depth = 0)
    {
        if (Overrides.TryGetValue(t, out var o))
            return o;
        if (t == typeof(TimeProvider))
            return TimeProvider.System;
        if (
            t.IsGenericType
            && t.GetGenericTypeDefinition().FullName == "Microsoft.Extensions.Logging.ILogger`1"
        )
            return Activator.CreateInstance(
                typeof(NullLogger<>).MakeGenericType(t.GetGenericArguments()[0])
            );
        if (t.FullName == "Microsoft.Extensions.Logging.ILogger")
            return NullLogger.Instance;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IOptions<>))
        {
            var inner = Activator.CreateInstance(t.GetGenericArguments()[0])!;
            return typeof(Options)
                .GetMethod(nameof(Options.Create))!
                .MakeGenericMethod(t.GetGenericArguments()[0])
                .Invoke(null, [inner]);
        }
        if (depth > 4)
            return t.IsInterface ? fakes.Create(t) : null;

        if (t.IsInterface || t.IsAbstract)
        {
            if (t.Name.EndsWith("Grain", StringComparison.Ordinal) || t.Name == "IGrainFactory")
                return fakes.Create(t);
            var impls = TurboAssemblies
                .SelectMany(SafeTypes)
                .Where(x =>
                    x.IsClass && !x.IsAbstract && t.IsAssignableFrom(x) && !x.Name.Contains("Grain")
                )
                .ToList();
            foreach (var impl in impls)
            {
                var built = TryBuild(impl, depth + 1);
                if (built is not null)
                    return built;
            }
            return t.IsInterface ? fakes.Create(t) : null;
        }

        return TryBuild(t, depth + 1);
    }

    private object? TryBuild(Type t, int depth)
    {
        foreach (
            var ctor in t.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
                .OrderByDescending(c => c.GetParameters().Length)
        )
        {
            try
            {
                var args = ctor.GetParameters()
                    .Select(p => Resolve(p.ParameterType, depth))
                    .ToArray();
                return ctor.Invoke(args);
            }
            catch (Exception)
            {
                // try the next constructor
            }
        }
        return null;
    }

    private static IEnumerable<Type> SafeTypes(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(x => x is not null)!;
        }
    }
}
