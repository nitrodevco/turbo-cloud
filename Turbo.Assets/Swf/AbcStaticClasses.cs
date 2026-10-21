using System;
using System.Collections.Generic;
using System.Text;

namespace Turbo.Assets.Swf;

/// <summary>
/// The classes a class's static initializer puts in its static fields, read from an SWF's
/// ActionScript (an ABC block of a DoABC tag). This is how an <c>[Embed]</c> lands in a compiled
/// client: Habbo's <c>HabboWindowManagerCom</c> declares
/// <c>[Embed(source="...")] public static var cursed_dragon_adoption_icon:Class</c>, and the compiler
/// makes the bitmap a class of its own (<c>icon_png$547e…</c>, the name the symbol table gives the
/// image) and assigns it in the static initializer - <c>getlex icon_png$547e…</c>,
/// <c>findproperty</c>, <c>swap</c>, <c>setproperty cursed_dragon_adoption_icon</c>.
/// <para>
/// Only what that takes is read (AVM2 overview, chapter 4): the constant pool's strings and names,
/// the classes' names and static initializers, and the method bodies; everything else is skipped.
/// </para>
/// </summary>
public static class AbcStaticClasses
{
    private const int TRAIT_SLOT = 0;
    private const int TRAIT_CONST = 6;
    private const int ATTR_METADATA = 0x40;
    private const int INSTANCE_PROTECTED_NS = 0x08;
    private const int METHOD_HAS_OPTIONAL = 0x08;
    private const int METHOD_HAS_PARAM_NAMES = 0x80;

    private const byte OP_SWAP = 0x2b;
    private const byte OP_FIND_PROP_STRICT = 0x5d;
    private const byte OP_FIND_PROPERTY = 0x5e;
    private const byte OP_GET_LEX = 0x60;
    private const byte OP_SET_PROPERTY = 0x61;
    private const byte OP_INIT_PROPERTY = 0x68;
    private const byte OP_PUSH_BYTE = 0x24;
    private const byte OP_GET_SCOPE_OBJECT = 0x65;
    private const byte OP_LOOKUP_SWITCH = 0x1b;
    private const byte OP_DEBUG = 0xef;

    /// <summary>The instructions with one u30 operand.</summary>
    private static readonly HashSet<byte> ONE_U30 =
    [
        0x04, // getsuper
        0x05, // setsuper
        0x06, // dxns
        0x08, // kill
        0x25, // pushshort
        0x2c, // pushstring
        0x2d, // pushint
        0x2e, // pushuint
        0x2f, // pushdouble
        0x31, // pushnamespace
        0x40, // newfunction
        0x41, // call
        0x42, // construct
        0x49, // constructsuper
        0x53, // applytype
        0x55, // newobject
        0x56, // newarray
        0x58, // newclass
        0x59, // getdescendants
        0x5a, // newcatch
        0x5d, // findpropstrict
        0x5e, // findproperty
        0x5f, // finddef
        0x60, // getlex
        0x61, // setproperty
        0x62, // getlocal
        0x63, // setlocal
        0x66, // getproperty
        0x68, // initproperty
        0x6a, // deleteproperty
        0x6c, // getslot
        0x6d, // setslot
        0x6e, // getglobalslot
        0x6f, // setglobalslot
        0x80, // coerce
        0x86, // astype
        0xb2, // istype
        0xc2, // inclocal
        0xc3, // declocal
        0xc6, // inclocal_i
        0xc7, // declocal_i
        0xf0, // debugline
        0xf1, // debugfile
    ];

    /// <summary>The instructions with two u30 operands.</summary>
    private static readonly HashSet<byte> TWO_U30 =
    [
        0x32, // hasnext2
        0x43, // callmethod
        0x44, // callstatic
        0x45, // callsuper
        0x46, // callproperty
        0x4a, // constructprop
        0x4c, // callproplex
        0x4e, // callsupervoid
        0x4f, // callpropvoid
    ];

    /// <summary>
    /// For each class <paramref name="wanted"/> takes (by its name, without a package), the class each
    /// static field is set to in its static initializer, by field name.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> Read(
        ReadOnlySpan<byte> abc,
        Func<string, bool> wanted
    )
    {
        var reader = new Reader(abc);
        var names = ReadConstantPool(ref reader);

        SkipMethods(ref reader);
        SkipMetadata(ref reader);

        var classCount = reader.U30();
        var classNames = new string?[classCount];

        for (var i = 0; i < classCount; i++)
        {
            classNames[i] = names.At(reader.U30());
            reader.U30(); // super name

            var flags = reader.U8();

            if ((flags & INSTANCE_PROTECTED_NS) != 0)
                reader.U30();

            var interfaces = reader.U30();

            for (var j = 0; j < interfaces; j++)
                reader.U30();

            reader.U30(); // instance initializer
            SkipTraits(ref reader);
        }

        var initializers = new Dictionary<int, string>();

        for (var i = 0; i < classCount; i++)
        {
            var cinit = reader.U30();

            SkipTraits(ref reader);

            if (classNames[i] is { } name && wanted(name))
                initializers.TryAdd(cinit, name);
        }

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        if (initializers.Count == 0)
            return result;

        var scripts = reader.U30();

        for (var i = 0; i < scripts; i++)
        {
            reader.U30();
            SkipTraits(ref reader);
        }

        var bodies = reader.U30();

        for (var i = 0; i < bodies; i++)
        {
            var method = reader.U30();

            reader.U30(); // max stack
            reader.U30(); // locals
            reader.U30(); // init scope depth
            reader.U30(); // max scope depth

            var code = reader.Bytes(reader.U30());
            var exceptions = reader.U30();

            for (var j = 0; j < exceptions * 5; j++)
                reader.U30();

            SkipTraits(ref reader);

            if (initializers.TryGetValue(method, out var className))
                result[className] = Assignments(code, names);
        }

        return result;
    }

    /// <summary>Each <c>getlex C</c> … <c>setproperty f</c> / <c>initproperty f</c> of a method's code, as f to C.</summary>
    private static Dictionary<string, string> Assignments(ReadOnlySpan<byte> code, string?[] names)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var reader = new Reader(code);
        string? loaded = null;

        while (!reader.AtEnd)
        {
            var op = reader.U8();

            switch (op)
            {
                case OP_GET_LEX:
                    loaded = names.At(reader.U30());

                    continue;
                case OP_SET_PROPERTY or OP_INIT_PROPERTY:
                {
                    var field = names.At(reader.U30());

                    if (loaded is not null && field is not null)
                        found[field] = loaded;

                    loaded = null;

                    continue;
                }
                // The field's object is found and swapped under the class: the class stays loaded.
                case OP_FIND_PROPERTY or OP_FIND_PROP_STRICT:
                    reader.U30();

                    continue;
                case OP_SWAP:
                    continue;
            }

            loaded = null;

            if (ONE_U30.Contains(op))
                reader.U30();
            else if (TWO_U30.Contains(op))
            {
                reader.U30();
                reader.U30();
            }
            else if (op is >= 0x0c and <= 0x1a) // the branches: an s24 offset
                reader.Skip(3);
            else if (op == OP_LOOKUP_SWITCH)
            {
                reader.Skip(3);
                reader.Skip(3 * (reader.U30() + 1));
            }
            else if (op is OP_PUSH_BYTE or OP_GET_SCOPE_OBJECT)
                reader.Skip(1);
            else if (op == OP_DEBUG)
            {
                reader.Skip(1);
                reader.U30();
                reader.Skip(1);
                reader.U30();
            }
        }

        return found;
    }

    /// <summary>The constant pool, kept as each multiname's own name (null for one without).</summary>
    private static string?[] ReadConstantPool(ref Reader reader)
    {
        reader.Skip(4); // minor, major version

        var ints = reader.U30();

        for (var i = 1; i < ints; i++)
            reader.U30();

        var uints = reader.U30();

        for (var i = 1; i < uints; i++)
            reader.U30();

        var doubles = reader.U30();

        reader.Skip(8 * Math.Max(0, doubles - 1));

        var stringCount = reader.U30();
        var strings = new string?[Math.Max(1, stringCount)];

        for (var i = 1; i < stringCount; i++)
            strings[i] = Encoding.UTF8.GetString(reader.Bytes(reader.U30()));

        var namespaces = reader.U30();

        for (var i = 1; i < namespaces; i++)
        {
            reader.U8();
            reader.U30();
        }

        var namespaceSets = reader.U30();

        for (var i = 1; i < namespaceSets; i++)
        {
            var count = reader.U30();

            for (var j = 0; j < count; j++)
                reader.U30();
        }

        var multinameCount = reader.U30();
        var names = new string?[Math.Max(1, multinameCount)];

        for (var i = 1; i < multinameCount; i++)
        {
            var kind = reader.U8();

            switch (kind)
            {
                case 0x07 or 0x0d: // QName(A): namespace, name
                    reader.U30();
                    names[i] = strings.At(reader.U30());
                    break;
                case 0x0f or 0x10: // RTQName(A): name
                    names[i] = strings.At(reader.U30());
                    break;
                case 0x11 or 0x12: // RTQNameL(A)
                    break;
                case 0x09 or 0x0e: // Multiname(A): name, namespace set
                    names[i] = strings.At(reader.U30());
                    reader.U30();
                    break;
                case 0x1b or 0x1c: // MultinameL(A): namespace set
                    reader.U30();
                    break;
                case 0x1d: // a generic type: the type, its parameters
                {
                    reader.U30();

                    var parameters = reader.U30();

                    for (var j = 0; j < parameters; j++)
                        reader.U30();

                    break;
                }
                default:
                    throw new AssetFormatException(
                        $"The SWF's ActionScript has a name of an unknown kind ({kind})."
                    );
            }
        }

        return names;
    }

    private static void SkipMethods(ref Reader reader)
    {
        var count = reader.U30();

        for (var i = 0; i < count; i++)
        {
            var parameters = reader.U30();

            reader.U30(); // return type

            for (var j = 0; j < parameters; j++)
                reader.U30();

            reader.U30(); // name

            var flags = reader.U8();

            if ((flags & METHOD_HAS_OPTIONAL) != 0)
            {
                var options = reader.U30();

                for (var j = 0; j < options; j++)
                {
                    reader.U30();
                    reader.U8();
                }
            }

            if ((flags & METHOD_HAS_PARAM_NAMES) != 0)
                for (var j = 0; j < parameters; j++)
                    reader.U30();
        }
    }

    private static void SkipMetadata(ref Reader reader)
    {
        var count = reader.U30();

        for (var i = 0; i < count; i++)
        {
            reader.U30();

            var items = reader.U30();

            for (var j = 0; j < items * 2; j++)
                reader.U30();
        }
    }

    private static void SkipTraits(ref Reader reader)
    {
        var count = reader.U30();

        for (var i = 0; i < count; i++)
        {
            reader.U30(); // name

            var kind = reader.U8();

            switch (kind & 0x0f)
            {
                case TRAIT_SLOT or TRAIT_CONST:
                    reader.U30(); // slot id
                    reader.U30(); // type

                    if (reader.U30() != 0) // value index, then its kind
                        reader.U8();

                    break;
                default: // class, function, method, getter, setter: a slot or dispatch id, and an index
                    reader.U30();
                    reader.U30();
                    break;
            }

            if ((kind & ATTR_METADATA) != 0)
            {
                var metadata = reader.U30();

                for (var j = 0; j < metadata; j++)
                    reader.U30();
            }
        }
    }

    private static T? At<T>(this T[] array, int index) =>
        index > 0 && index < array.Length ? array[index] : default;

    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _offset;

        public readonly bool AtEnd => _offset >= _data.Length;

        public byte U8()
        {
            if (_offset >= _data.Length)
                throw Truncated();

            return _data[_offset++];
        }

        /// <summary>A variable-length integer: seven bits a byte, low first, up to five bytes.</summary>
        public int U30()
        {
            var value = 0;

            for (var shift = 0; shift < 35; shift += 7)
            {
                var b = U8();

                value |= (b & 0x7f) << shift;

                if ((b & 0x80) == 0)
                    break;
            }

            return value & 0x3fffffff;
        }

        public void Skip(int count)
        {
            if (count < 0 || _offset + count > _data.Length)
                throw Truncated();

            _offset += count;
        }

        public ReadOnlySpan<byte> Bytes(int count)
        {
            if (count < 0 || _offset + count > _data.Length)
                throw Truncated();

            var bytes = _data.Slice(_offset, count);

            _offset += count;

            return bytes;
        }

        private static AssetFormatException Truncated() =>
            new("The SWF's ActionScript is truncated.");
    }
}
