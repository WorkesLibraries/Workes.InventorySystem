using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Process-wide registry and resolver for portable snapshot value codecs.
/// </summary>
/// <summary>Encodes and decodes the package-supported portable scalar, array, and list value shapes.</summary>
public static class InventorySnapshotCodecs
{
    internal const string ReservedPrefix = "workes.inventory.";
    internal const string NullCodecId = ReservedPrefix + "value.null";

    private static readonly object s_gate = new();
    private static readonly Dictionary<Type, ICodecAdapter> s_byType = new();
    private static readonly Dictionary<string, ICodecAdapter> s_byId = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, string> s_customKeyIdsByType = new();
    private static readonly Dictionary<string, Type> s_customKeyTypesById = new(StringComparer.Ordinal);

    [ThreadStatic]
    private static HashSet<object>? t_encodingPath;

    static InventorySnapshotCodecs()
    {
        RegisterBuiltIns();
    }

    /// <summary>
    /// Attempts to encode a value through the codec assigned to its exact CLR type.
    /// </summary>
    public static bool TryEncode<T>(
        T value,
        out SnapshotEncodedValue? encoded,
        out InventoryFailure? failure)
    {
        return TryEncodeObject(value, out encoded, out failure);
    }

    /// <summary>
    /// Encodes a value or throws when its exact CLR type is unsupported.
    /// </summary>
    public static SnapshotEncodedValue Encode<T>(T value)
    {
        if (!TryEncode(value, out var encoded, out var failure) || encoded == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
        return encoded;
    }

    /// <summary>
    /// Attempts to decode an encoded value as an exact CLR type.
    /// </summary>
    public static bool TryDecode<T>(
        SnapshotEncodedValue encoded,
        out T value,
        out InventoryFailure? failure)
    {
        value = default!;
        if (!SnapshotValueValidator.TryCloneEncoded(encoded, out var detached, out failure) || detached == null)
            return false;

        if (detached.CodecId == NullCodecId)
        {
            if (default(T) is not null)
            {
                failure = InventoryFailures.Snapshot($"Snapshot null cannot be decoded as non-nullable type '{typeof(T).FullName}'.");
                return false;
            }

            failure = null;
            return true;
        }

        if (!TryGetAdapter(typeof(T), out var adapter, out failure) || adapter == null)
            return false;
        if (!string.Equals(adapter.FormatId, detached.CodecId, StringComparison.Ordinal))
        {
            failure =
                InventoryFailures.SnapshotCodecRejected(
                    $"Snapshot codec '{detached.CodecId}' does not match the assigned codec " +
                    $"'{adapter.FormatId}' for type '{typeof(T).FullName}'.");
            return false;
        }
        if (detached.Data.Kind == SnapshotValueKind.Null)
        {
            if (default(T) is not null)
            {
                failure = InventoryFailures.Snapshot($"Snapshot null cannot be decoded as non-nullable type '{typeof(T).FullName}'.");
                return false;
            }
            failure = null;
            return true;
        }

        object? decoded;
        try
        {
            if (!adapter.TryDecode(detached.Data, detached.CodecVersion, out decoded, out failure))
            {
                failure ??= InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' rejected version {detached.CodecVersion} data.");
                return false;
            }
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' failed to decode data: {ex.Message}");
            return false;
        }
        if (decoded is not T typed)
        {
            failure = InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' returned an incompatible value.");
            return false;
        }

        value = typed;
        return true;
    }

    internal static bool TryDecodeRuntime(
        SnapshotEncodedValue encoded,
        out object? value,
        out InventoryFailure? failure)
    {
        return TryDecodeRuntime(encoded, out value, out _, out failure);
    }

    internal static bool TryDecodeRuntime(
        SnapshotEncodedValue encoded,
        out object? value,
        out Type? valueType,
        out InventoryFailure? failure)
    {
        value = null;
        valueType = null;
        if (!SnapshotValueValidator.TryCloneEncoded(encoded, out var detached, out failure) || detached == null)
            return false;
        if (detached.CodecId == NullCodecId)
        {
            failure = null;
            return true;
        }

        if (!TryGetAdapterById(detached.CodecId, out var adapter, out failure) || adapter == null)
            return false;
        valueType = adapter.ValueType;
        if (detached.Data.Kind == SnapshotValueKind.Null)
        {
            if (adapter.ValueType.IsValueType && Nullable.GetUnderlyingType(adapter.ValueType) == null)
            {
                failure = InventoryFailures.Snapshot($"Snapshot null cannot be decoded as non-nullable type '{adapter.ValueType.FullName}'.");
                return false;
            }
            value = null;
            failure = null;
            return true;
        }

        try
        {
            if (!adapter.TryDecode(detached.Data, detached.CodecVersion, out value, out failure))
            {
                failure ??= InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' rejected version {detached.CodecVersion} data.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            value = null;
                failure = InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' failed to decode data: {ex.Message}");
                return false;
        }
    }

    internal static bool IsCodecResolvable(string formatId)
    {
        return formatId == NullCodecId ||
               TryGetAdapterById(formatId, out _, out _);
    }

    internal static bool TryEncodeObject(
        object? value,
        out SnapshotEncodedValue? encoded,
        out InventoryFailure? failure)
    {
        encoded = null;
        if (value == null)
        {
            encoded = new SnapshotEncodedValue
            {
                CodecId = NullCodecId,
                CodecVersion = 1,
                Data = SnapshotValue.Null()
            };
            failure = null;
            return true;
        }

        var type = value.GetType();
        if (type == typeof(object))
        {
            failure = InventoryFailures.Snapshot("Literal System.Object values are not portable snapshot values.");
            return false;
        }
        if (!TryGetAdapter(type, out var adapter, out failure) || adapter == null)
            return false;

        bool trackReference = !type.IsValueType && type != typeof(string);
        var path = t_encodingPath ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (trackReference && !path.Add(value))
        {
            failure = InventoryFailures.Snapshot($"Snapshot value graph contains a cycle at type '{type.FullName}'.");
            return false;
        }

        try
        {
            if (!adapter.TryEncode(value, out var data, out failure) || data == null)
            {
                failure ??= InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' rejected the value.");
                return false;
            }
            if (!SnapshotValueValidator.TryClone(data, out var detached, out failure) || detached == null)
            {
                failure = InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' produced invalid data: {failure}");
                return false;
            }

            encoded = new SnapshotEncodedValue
            {
                CodecId = adapter.FormatId,
                CodecVersion = adapter.CurrentVersion,
                Data = detached
            };
            return true;
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Snapshot($"Snapshot codec '{adapter.FormatId}' failed to encode '{type.FullName}': {ex.Message}");
            return false;
        }
        finally
        {
            if (trackReference)
                path.Remove(value);
            if (path.Count == 0)
                t_encodingPath = null;
        }
    }

    internal static bool TryEncodeKey<TKey>(
        TKey value,
        out SnapshotEncodedValue? encoded,
        out InventoryFailure? failure)
    {
        if (value is null)
        {
            encoded = null;
            failure = InventoryFailures.Definition("Inventory definition ids cannot be null.");
            return false;
        }

        if (HasRegisteredAdapter(typeof(TKey)))
            return TryEncode(value, out encoded, out failure);

        return CustomKeyCodecCache<TKey>.TryEncode(value, out encoded, out failure);
    }

    internal static bool TryDecodeKey<TKey>(
        SnapshotEncodedValue encoded,
        out TKey value,
        out InventoryFailure? failure)
    {
        if (HasRegisteredAdapter(typeof(TKey)))
            return TryDecode(encoded, out value, out failure);

        return CustomKeyCodecCache<TKey>.TryDecode(encoded, out value, out failure);
    }

    private static bool HasRegisteredAdapter(Type type)
    {
        lock (s_gate)
            return s_byType.ContainsKey(type);
    }

    internal static bool TryEncodeDeclared(
        object? value,
        Type declaredType,
        out SnapshotEncodedValue? encoded,
        out InventoryFailure? failure)
    {
        if (value != null)
            return TryEncodeObject(value, out encoded, out failure);
        if (declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) == null)
        {
            encoded = null;
            failure = InventoryFailures.Snapshot($"Null cannot be encoded as non-nullable declared type '{declaredType.FullName}'.");
            return false;
        }
        if (!TryGetAdapter(declaredType, out var adapter, out failure) || adapter == null)
        {
            encoded = null;
            return false;
        }
        encoded = new SnapshotEncodedValue
        {
            CodecId = adapter.FormatId,
            CodecVersion = adapter.CurrentVersion,
            Data = SnapshotValue.Null()
        };
        return true;
    }

    private static bool TryGetAdapter(Type type, out ICodecAdapter? adapter, out InventoryFailure? failure)
    {
        lock (s_gate)
        {
            if (s_byType.TryGetValue(type, out adapter))
            {
                failure = null;
                return true;
            }

            if (type == typeof(object))
            {
                failure = InventoryFailures.Snapshot("Literal System.Object values are not portable snapshot values.");
                return false;
            }

            if (type.IsEnum)
            {
                failure = InventoryFailures.Snapshot($"Snapshot values of enum type '{type.FullName}' are unsupported.");
                return false;
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    failure = InventoryFailures.Snapshot($"Only one-dimensional snapshot arrays are supported; '{type.FullName}' has rank {type.GetArrayRank()}.");
                    return false;
                }

                return TryCreateCollectionAdapter(
                    typeof(ArrayCodec<>),
                    type.GetElementType()!,
                    out adapter,
                    out failure);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return TryCreateCollectionAdapter(
                    typeof(ListCodec<>),
                    type.GetGenericArguments()[0],
                    out adapter,
                    out failure);
            }

            failure = InventoryFailures.Snapshot($"CLR type '{type.FullName}' is not a supported portable snapshot value.");
            return false;
        }
    }

    private static bool TryCreateCollectionAdapter(
        Type openCodecType,
        Type elementType,
        out ICodecAdapter? adapter,
        out InventoryFailure? failure)
    {
        try
        {
            var codecType = openCodecType.MakeGenericType(elementType);
            adapter = (ICodecAdapter)Activator.CreateInstance(codecType)!;
            RegisterAdapter(adapter, allowReservedId: true);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            adapter = null;
            var cause = ex is System.Reflection.TargetInvocationException && ex.InnerException != null
                ? ex.InnerException
                : ex;
            failure = InventoryFailures.Snapshot($"Snapshot collection element type '{elementType.FullName}' is unsupported: {cause.Message}");
            return false;
        }
    }

    private static bool TryGetAdapterById(
        string formatId,
        out ICodecAdapter? adapter,
        out InventoryFailure? failure)
    {
        lock (s_gate)
        {
            if (s_byId.TryGetValue(formatId, out adapter))
            {
                failure = null;
                return true;
            }

            var collectionKinds = new[]
            {
                (prefix: ReservedPrefix + "value.array.", codecType: typeof(ArrayCodec<>)),
                (prefix: ReservedPrefix + "value.list.", codecType: typeof(ListCodec<>))
            };
            foreach (var candidate in collectionKinds)
            {
                if (!formatId.StartsWith(candidate.prefix, StringComparison.Ordinal))
                    continue;

                string childId = Uri.UnescapeDataString(formatId.Substring(candidate.prefix.Length));
                if (!TryGetAdapterById(childId, out var childAdapter, out failure) || childAdapter == null)
                {
                    adapter = null;
                    return false;
                }

                try
                {
                    var closedCodecType = candidate.codecType.MakeGenericType(childAdapter.ValueType);
                    adapter = (ICodecAdapter)Activator.CreateInstance(closedCodecType)!;
                }
                catch (Exception ex)
                {
                    adapter = null;
                    failure = InventoryFailures.Snapshot($"Snapshot collection codec id '{formatId}' is malformed: {ex.Message}");
                    return false;
                }
                if (!string.Equals(adapter.FormatId, formatId, StringComparison.Ordinal))
                {
                    adapter = null;
                    failure = InventoryFailures.Snapshot($"Snapshot collection codec id '{formatId}' is malformed.");
                    return false;
                }
                RegisterAdapter(adapter, allowReservedId: true);
                failure = null;
                return true;
            }

            adapter = null;
            failure = InventoryFailures.Snapshot($"Snapshot codec '{formatId}' is not registered.");
            return false;
        }
    }

    private static void RegisterAdapter(ICodecAdapter adapter, bool allowReservedId)
    {
        if (string.IsNullOrWhiteSpace(adapter.FormatId))
            throw new ArgumentException("Snapshot codec format id cannot be null or empty.");
        if (adapter.CurrentVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(adapter), "Snapshot codec version must be positive.");
        if (!allowReservedId && adapter.FormatId.StartsWith(ReservedPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Snapshot codec id prefix '{ReservedPrefix}' is reserved by the package.");

        lock (s_gate)
        {
            if (s_byType.ContainsKey(adapter.ValueType))
                throw new InvalidOperationException($"A snapshot codec is already registered for CLR type '{adapter.ValueType.FullName}'.");
            if (s_byId.ContainsKey(adapter.FormatId))
                throw new InvalidOperationException($"Snapshot codec id '{adapter.FormatId}' is already registered.");

            s_byType.Add(adapter.ValueType, adapter);
            s_byId.Add(adapter.FormatId, adapter);
        }
    }

    private static void RegisterBuiltIns()
    {
        RegisterBuiltIn(new DelegateCodec<string>(
            "workes.inventory.value.string",
            value => SnapshotValue.String(value),
            RequireString));
        RegisterBuiltIn(new DelegateCodec<char>(
            "workes.inventory.value.char",
            value => SnapshotValue.String(((int)value).ToString("X4", CultureInfo.InvariantCulture)),
            (SnapshotValue value, out char decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code) ||
                    code < char.MinValue || code > char.MaxValue)
                    return Fail("Snapshot char payload is invalid.", out failure);
                decoded = (char)code;
                return true;
            }));
        RegisterBuiltIn(new DelegateCodec<bool>(
            "workes.inventory.value.boolean",
            SnapshotValue.Boolean,
            (SnapshotValue value, out bool decoded, out InventoryFailure? failure) =>
            {
                decoded = value.BooleanValue;
                if (value.Kind != SnapshotValueKind.Boolean)
                    return Fail("Snapshot Boolean payload is invalid.", out failure);
                failure = null;
                return true;
            }));

        RegisterInvariant<byte>("byte", byte.TryParse);
        RegisterInvariant<sbyte>("sbyte", sbyte.TryParse);
        RegisterInvariant<short>("int16", short.TryParse);
        RegisterInvariant<ushort>("uint16", ushort.TryParse);
        RegisterInvariant<int>("int32", int.TryParse);
        RegisterInvariant<uint>("uint32", uint.TryParse);
        RegisterInvariant<long>("int64", long.TryParse);
        RegisterInvariant<ulong>("uint64", ulong.TryParse);

        RegisterBuiltIn(new DelegateCodec<float>(
            "workes.inventory.value.single",
            value => SnapshotValue.String(
                BitConverter.ToUInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture)),
            (SnapshotValue value, out float decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bits))
                    return Fail("Snapshot Single payload is invalid.", out failure);
                decoded = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                return true;
            }));
        RegisterBuiltIn(new DelegateCodec<double>(
            "workes.inventory.value.double",
            value => SnapshotValue.String(
                unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString("X16", CultureInfo.InvariantCulture)),
            (SnapshotValue value, out double decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong bits))
                    return Fail("Snapshot Double payload is invalid.", out failure);
                decoded = BitConverter.Int64BitsToDouble(unchecked((long)bits));
                return true;
            }));
        RegisterBuiltIn(new DelegateCodec<decimal>(
            "workes.inventory.value.decimal",
            value => SnapshotValue.String(string.Join(
                ",",
                decimal.GetBits(value).Select(part => unchecked((uint)part).ToString("X8", CultureInfo.InvariantCulture)))),
            (SnapshotValue value, out decimal decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure))
                    return false;
                var parts = text.Split(',');
                var bits = new int[4];
                if (parts.Length != 4)
                    return Fail("Snapshot Decimal payload is invalid.", out failure);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!uint.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint part))
                        return Fail("Snapshot Decimal payload is invalid.", out failure);
                    bits[i] = unchecked((int)part);
                }
                try
                {
                    decoded = new decimal(bits);
                    failure = null;
                    return true;
                }
                catch (ArgumentException)
                {
                    return Fail("Snapshot Decimal payload is invalid.", out failure);
                }
            }));
        RegisterBuiltIn(new DelegateCodec<Guid>(
            "workes.inventory.value.guid",
            value => SnapshotValue.String(value.ToString("D")),
            (SnapshotValue value, out Guid decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !Guid.TryParseExact(text, "D", out decoded))
                    return Fail("Snapshot Guid payload is invalid.", out failure);
                failure = null;
                return true;
            }));
        RegisterBuiltIn(new DelegateCodec<DateTime>(
            "workes.inventory.value.datetime",
            value => SnapshotValue.String(
                value.Ticks.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)value.Kind).ToString(CultureInfo.InvariantCulture)),
            TryDecodeDateTime));
        RegisterBuiltIn(new DelegateCodec<DateTimeOffset>(
            "workes.inventory.value.datetime-offset",
            value => SnapshotValue.String(
                value.Ticks.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)value.Offset.TotalMinutes).ToString(CultureInfo.InvariantCulture)),
            TryDecodeDateTimeOffset));
        RegisterBuiltIn(new DelegateCodec<TimeSpan>(
            "workes.inventory.value.timespan",
            value => SnapshotValue.String(value.Ticks.ToString(CultureInfo.InvariantCulture)),
            (SnapshotValue value, out TimeSpan decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                    return Fail("Snapshot TimeSpan payload is invalid.", out failure);
                decoded = TimeSpan.FromTicks(ticks);
                return true;
            }));
        RegisterBuiltIn(new DynamicObjectCodec());
    }

    private static void RegisterInvariant<T>(string suffix, TryParseInvariant<T> parser)
        where T : struct, IFormattable
    {
        RegisterBuiltIn(new DelegateCodec<T>(
            ReservedPrefix + "value." + suffix,
            value => SnapshotValue.String(value.ToString(null, CultureInfo.InvariantCulture)),
            (SnapshotValue value, out T decoded, out InventoryFailure? failure) =>
            {
                decoded = default;
                if (!TryReadString(value, out var text, out failure) ||
                    !parser(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out decoded))
                    return Fail($"Snapshot {typeof(T).Name} payload is invalid.", out failure);
                failure = null;
                return true;
            }));
    }

    private static bool TryDecodeDateTime(
        SnapshotValue value,
        out DateTime decoded,
        out InventoryFailure? failure)
    {
        decoded = default;
        if (!TrySplitPair(value, out long ticks, out int kind, out failure) ||
            kind < (int)DateTimeKind.Unspecified ||
            kind > (int)DateTimeKind.Local)
            return Fail("Snapshot DateTime payload is invalid.", out failure);
        try
        {
            decoded = new DateTime(ticks, (DateTimeKind)kind);
            failure = null;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Fail("Snapshot DateTime payload is invalid.", out failure);
        }
    }

    private static bool TryDecodeDateTimeOffset(
        SnapshotValue value,
        out DateTimeOffset decoded,
        out InventoryFailure? failure)
    {
        decoded = default;
        if (!TrySplitPair(value, out long ticks, out int offsetMinutes, out failure))
            return false;
        try
        {
            decoded = new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
            failure = null;
            return true;
        }
        catch (ArgumentException)
        {
            return Fail("Snapshot DateTimeOffset payload is invalid.", out failure);
        }
    }

    private static bool TrySplitPair(
        SnapshotValue value,
        out long first,
        out int second,
        out InventoryFailure? failure)
    {
        first = default;
        second = default;
        if (!TryReadString(value, out var text, out failure))
            return false;
        var parts = text.Split(':');
        if (parts.Length != 2 ||
            !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out second))
            return Fail("Snapshot paired scalar payload is invalid.", out failure);
        return true;
    }

    private static bool RequireString(
        SnapshotValue value,
        out string decoded,
        out InventoryFailure? failure)
    {
        decoded = string.Empty;
        if (!TryReadString(value, out var text, out failure))
            return false;
        decoded = text;
        return true;
    }

    private static bool TryReadString(
        SnapshotValue value,
        out string text,
        out InventoryFailure? failure)
    {
        text = string.Empty;
        if (value.Kind != SnapshotValueKind.String || value.StringValue == null)
            return Fail("Snapshot string payload is invalid.", out failure);
        text = value.StringValue;
        failure = null;
        return true;
    }

    private static bool Fail(string message, out InventoryFailure? failure)
    {
        failure = InventoryFailures.SnapshotCodecRejected(message);
        return false;
    }

    private static string CollectionId(string kind, Type elementType)
    {
        if (!TryGetAdapter(elementType, out var adapter, out var failure) || adapter == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
        return ReservedPrefix + "value." + kind + "." + Uri.EscapeDataString(adapter.FormatId);
    }

    private static void RegisterBuiltIn<T>(ISnapshotValueCodec<T> codec) =>
        RegisterAdapter(new CodecAdapter<T>(codec), allowReservedId: true);

    private delegate bool TryParseInvariant<T>(
        string text,
        NumberStyles styles,
        IFormatProvider provider,
        out T value);

    private delegate bool TryDecodeDelegate<T>(
        SnapshotValue value,
        out T decoded,
        out InventoryFailure? failure);

    private interface ICodecAdapter
    {
        Type ValueType { get; }
        string FormatId { get; }
        int CurrentVersion { get; }
        bool TryEncode(object value, out SnapshotValue? encoded, out InventoryFailure? failure);
        bool TryDecode(SnapshotValue encoded, int version, out object? value, out InventoryFailure? failure);
    }

    private sealed class CodecAdapter<T> : ICodecAdapter
    {
        private readonly ISnapshotValueCodec<T> _codec;

        public CodecAdapter(ISnapshotValueCodec<T> codec)
        {
            _codec = codec;
        }

        public Type ValueType => typeof(T);
        public string FormatId => _codec.FormatId;
        public int CurrentVersion => _codec.CurrentVersion;

        public bool TryEncode(object value, out SnapshotValue? encoded, out InventoryFailure? failure)
        {
            if (value is not T typed)
            {
                encoded = null;
                failure = InventoryFailures.Snapshot($"Snapshot codec '{FormatId}' received an incompatible CLR value.");
                return false;
            }
            return _codec.TryEncode(typed, out encoded, out failure);
        }

        public bool TryDecode(SnapshotValue encoded, int version, out object? value, out InventoryFailure? failure)
        {
            value = null;
            if (!_codec.TryDecode(encoded, version, out var decoded, out failure))
                return false;
            value = decoded;
            return true;
        }
    }

    private sealed class DelegateCodec<T> : ISnapshotValueCodec<T>
    {
        private readonly Func<T, SnapshotValue> _encode;
        private readonly TryDecodeDelegate<T> _decode;

        public DelegateCodec(
            string formatId,
            Func<T, SnapshotValue> encode,
            TryDecodeDelegate<T> decode)
        {
            FormatId = formatId;
            _encode = encode;
            _decode = decode;
        }

        public string FormatId { get; }
        public int CurrentVersion => 1;

        public bool TryEncode(T value, out SnapshotValue? encoded, out InventoryFailure? failure)
        {
            encoded = _encode(value);
            failure = null;
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out T value, out InventoryFailure? failure)
        {
            value = default!;
            if (version != CurrentVersion)
                return Fail($"Snapshot codec '{FormatId}' does not support version {version}.", out failure);
            return _decode(encoded, out value, out failure);
        }
    }

    private sealed class DynamicObjectCodec : ISnapshotValueCodec<object>
    {
        public string FormatId => ReservedPrefix + "value.dynamic";
        public int CurrentVersion => 1;

        public bool TryEncode(object value, out SnapshotValue? encoded, out InventoryFailure? failure)
        {
            if (!TryEncodeObject(value, out var child, out failure) || child == null)
            {
                encoded = null;
                return false;
            }
            encoded = SnapshotValue.Object(new[]
            {
                new SnapshotNamedValue { Name = "value", Value = child }
            });
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out object value, out InventoryFailure? failure)
        {
            value = null!;
            if (version != CurrentVersion ||
                encoded.Kind != SnapshotValueKind.Object ||
                encoded.Properties.Count != 1 ||
                encoded.Properties[0].Name != "value")
                return Fail("Snapshot dynamic-object payload is invalid.", out failure);
            if (!TryDecodeRuntime(encoded.Properties[0].Value, out var decoded, out failure))
                return false;
            value = decoded!;
            return true;
        }
    }

    private sealed class ArrayCodec<T> : ICodecAdapter
    {
        public Type ValueType => typeof(T[]);
        public string FormatId { get; } = CollectionId("array", typeof(T));
        public int CurrentVersion => 1;

        public bool TryEncode(object value, out SnapshotValue? encoded, out InventoryFailure? failure)
        {
            var array = (T[])value;
            var items = new List<SnapshotEncodedValue>(array.Length);
            foreach (var item in array)
            {
                if (!TryEncodeObject(item, out var child, out failure) || child == null)
                {
                    encoded = null;
                    return false;
                }
                items.Add(child);
            }
            encoded = SnapshotValue.List(items);
            failure = null;
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out object? value, out InventoryFailure? failure)
        {
            value = null;
            if (version != CurrentVersion || encoded.Kind != SnapshotValueKind.List)
                return Fail($"Snapshot array codec '{FormatId}' received invalid data.", out failure);
            var result = new T[encoded.Items.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (!TryDecodeElement(encoded.Items[i], out result[i], out failure))
                    return false;
            }
            value = result;
            failure = null;
            return true;
        }
    }

    private sealed class ListCodec<T> : ICodecAdapter
    {
        public Type ValueType => typeof(List<T>);
        public string FormatId { get; } = CollectionId("list", typeof(T));
        public int CurrentVersion => 1;

        public bool TryEncode(object value, out SnapshotValue? encoded, out InventoryFailure? failure)
        {
            var list = (List<T>)value;
            var items = new List<SnapshotEncodedValue>(list.Count);
            foreach (var item in list)
            {
                if (!TryEncodeObject(item, out var child, out failure) || child == null)
                {
                    encoded = null;
                    return false;
                }
                items.Add(child);
            }
            encoded = SnapshotValue.List(items);
            failure = null;
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out object? value, out InventoryFailure? failure)
        {
            value = null;
            if (version != CurrentVersion || encoded.Kind != SnapshotValueKind.List)
                return Fail($"Snapshot list codec '{FormatId}' received invalid data.", out failure);
            var result = new List<T>(encoded.Items.Count);
            foreach (var child in encoded.Items)
            {
                if (!TryDecodeElement(child, out T item, out failure))
                    return false;
                result.Add(item);
            }
            value = result;
            failure = null;
            return true;
        }
    }

    private static bool TryDecodeElement<T>(
        SnapshotEncodedValue encoded,
        out T value,
        out InventoryFailure? failure)
    {
        value = default!;
        if (!TryDecodeRuntime(encoded, out var decoded, out failure))
            return false;
        if (decoded == null)
        {
            if (default(T) is not null)
                return Fail($"Snapshot null cannot be assigned to collection element type '{typeof(T).FullName}'.", out failure);
            return true;
        }
        if (decoded is not T typed)
            return Fail($"Snapshot value type '{decoded.GetType().FullName}' cannot be assigned to '{typeof(T).FullName}'.", out failure);
        value = typed;
        return true;
    }

    private static class CustomKeyCodecCache<TKey>
    {
        private static readonly IInventorySnapshotKeyCodec<TKey>? s_codec;
        private static readonly InventoryFailure? s_configurationError;

        static CustomKeyCodecCache()
        {
            var attribute = typeof(TKey).GetCustomAttribute<InventorySnapshotKeyCodecAttribute>(inherit: false);
            if (attribute == null)
            {
                s_configurationError =
                    InventoryFailures.SnapshotMissingCodec(
                        $"Custom inventory key type '{typeof(TKey).FullName}' must declare " +
                        $"InventorySnapshotKeyCodecAttribute.");
                return;
            }
            if (!typeof(IInventorySnapshotKeyCodec<TKey>).IsAssignableFrom(attribute.CodecType))
            {
                s_configurationError =
                    InventoryFailures.SnapshotCodecRejected(
                        $"Snapshot key codec '{attribute.CodecType.FullName}' does not implement " +
                        $"IInventorySnapshotKeyCodec<{typeof(TKey).FullName}>.");
                return;
            }

            try
            {
                s_codec = (IInventorySnapshotKeyCodec<TKey>?)Activator.CreateInstance(attribute.CodecType);
            }
            catch (Exception ex)
            {
                s_configurationError =
                    InventoryFailures.SnapshotCodecRejected(
                        $"Snapshot key codec '{attribute.CodecType.FullName}' could not be created: {ex.Message}");
                return;
            }

            if (s_codec == null || s_configurationError != null)
                s_configurationError = InventoryFailures.SnapshotCodecRejected($"Snapshot key codec '{attribute.CodecType.FullName}' could not be created.");
            else if (string.IsNullOrWhiteSpace(s_codec.FormatId))
                s_configurationError = InventoryFailures.SnapshotCodecRejected("Snapshot key codec format id cannot be null or empty.");
            else if (s_codec.FormatId.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                s_configurationError = InventoryFailures.SnapshotCodecRejected($"Snapshot key codec id prefix '{ReservedPrefix}' is reserved by the package.");
            else if (s_codec.CurrentVersion <= 0)
                s_configurationError = InventoryFailures.SnapshotCodecRejected("Snapshot key codec version must be positive.");
            else
            {
                lock (s_gate)
                {
                    if (s_customKeyIdsByType.TryGetValue(typeof(TKey), out var existingId) &&
                        !string.Equals(existingId, s_codec.FormatId, StringComparison.Ordinal))
                    {
                        s_configurationError =
                            InventoryFailures.SnapshotCodecRejected($"Custom key type '{typeof(TKey).FullName}' is already associated with codec id '{existingId}'.");
                    }
                    else if (s_customKeyTypesById.TryGetValue(s_codec.FormatId, out var existingType) &&
                             existingType != typeof(TKey))
                    {
                        s_configurationError =
                            InventoryFailures.SnapshotCodecRejected(
                                $"Snapshot key codec id '{s_codec.FormatId}' is already associated with " +
                                $"'{existingType.FullName}'.");
                    }
                    else
                    {
                        s_customKeyIdsByType[typeof(TKey)] = s_codec.FormatId;
                        s_customKeyTypesById[s_codec.FormatId] = typeof(TKey);
                    }
                }
            }
        }

        internal static bool TryEncode(
            TKey value,
            out SnapshotEncodedValue? encoded,
            out InventoryFailure? failure)
        {
            encoded = null;
            if (s_codec == null || s_configurationError != null)
            {
                failure = s_configurationError ?? InventoryFailures.SnapshotMissingCodec();
                return false;
            }

            try
            {
                if (!s_codec.TryEncode(value, out var data, out failure) || data == null)
                {
                    failure ??= InventoryFailures.Snapshot($"Snapshot key codec '{s_codec.FormatId}' rejected the key.");
                    return false;
                }
                if (!SnapshotValueValidator.TryClone(data, out var detached, out failure) || detached == null)
                {
                    failure = InventoryFailures.Snapshot($"Snapshot key codec '{s_codec.FormatId}' produced invalid data: {failure}");
                    return false;
                }
                encoded = new SnapshotEncodedValue
                {
                    CodecId = s_codec.FormatId,
                    CodecVersion = s_codec.CurrentVersion,
                    Data = detached
                };
                return true;
            }
            catch (Exception ex)
            {
                failure = InventoryFailures.Snapshot($"Snapshot key codec '{s_codec.FormatId}' failed to encode a key: {ex.Message}");
                return false;
            }
        }

        internal static bool TryDecode(
            SnapshotEncodedValue encoded,
            out TKey value,
            out InventoryFailure? failure)
        {
            value = default!;
            if (s_codec == null)
            {
                failure = s_configurationError ?? InventoryFailures.SnapshotMissingCodec();
                return false;
            }
            if (!SnapshotValueValidator.TryCloneEncoded(encoded, out var detached, out failure) || detached == null)
                return false;
            if (!string.Equals(detached.CodecId, s_codec.FormatId, StringComparison.Ordinal))
            {
                failure = InventoryFailures.Snapshot($"Snapshot codec '{detached.CodecId}' does not match key codec '{s_codec.FormatId}'.");
                return false;
            }
            try
            {
                if (!s_codec.TryDecode(detached.Data, detached.CodecVersion, out value, out failure))
                {
                    failure ??= InventoryFailures.Snapshot($"Snapshot key codec '{s_codec.FormatId}' rejected version {detached.CodecVersion}.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                failure = InventoryFailures.Snapshot($"Snapshot key codec '{s_codec.FormatId}' failed to decode a key: {ex.Message}");
                return false;
            }
        }
    }
}

internal static class SnapshotValueValidator
{
    public static bool TryClone(
        SnapshotValue? value,
        out SnapshotValue? clone,
        out InventoryFailure? failure)
    {
        return TryClone(value, new HashSet<object>(ReferenceEqualityComparer.Instance), out clone, out failure);
    }

    public static bool TryCloneEncoded(
        SnapshotEncodedValue? value,
        out SnapshotEncodedValue? clone,
        out InventoryFailure? failure)
    {
        return TryCloneEncoded(value, new HashSet<object>(ReferenceEqualityComparer.Instance), out clone, out failure);
    }

    private static bool TryClone(
        SnapshotValue? value,
        HashSet<object> path,
        out SnapshotValue? clone,
        out InventoryFailure? failure)
    {
        clone = null;
        if (value == null)
        {
            failure = InventoryFailures.Snapshot("Snapshot value cannot be null.");
            return false;
        }
        if (!path.Add(value))
        {
            failure = InventoryFailures.Snapshot("Snapshot value graph contains a cycle.");
            return false;
        }
        if (value.Items == null || value.Properties == null)
        {
            path.Remove(value);
            failure = InventoryFailures.Snapshot("Snapshot value collections cannot be null.");
            return false;
        }

        try
        {
            clone = new SnapshotValue
            {
                Kind = value.Kind,
                BooleanValue = value.BooleanValue,
                StringValue = value.StringValue
            };

            switch (value.Kind)
            {
                case SnapshotValueKind.Null:
                    if (value.StringValue != null || value.Items.Count != 0 || value.Properties.Count != 0)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot null value contains incompatible payload data.");
                        return false;
                    }
                    break;
                case SnapshotValueKind.Boolean:
                    if (value.StringValue != null || value.Items.Count != 0 || value.Properties.Count != 0)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot Boolean value contains incompatible payload data.");
                        return false;
                    }
                    break;
                case SnapshotValueKind.String:
                    if (value.StringValue == null)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot string value cannot be null.");
                        return false;
                    }
                    if (value.Items.Count != 0 || value.Properties.Count != 0)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot string value contains incompatible child data.");
                        return false;
                    }
                    break;
                case SnapshotValueKind.List:
                    if (value.Items == null)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot list items cannot be null.");
                        return false;
                    }
                    if (value.StringValue != null || value.Properties.Count != 0)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot list value contains incompatible payload data.");
                        return false;
                    }
                    foreach (var item in value.Items)
                    {
                        if (!TryCloneEncoded(item, path, out var itemClone, out failure) || itemClone == null)
                            return false;
                        clone.Items.Add(itemClone);
                    }
                    break;
                case SnapshotValueKind.Object:
                    if (value.Properties == null)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot object properties cannot be null.");
                        return false;
                    }
                    if (value.StringValue != null || value.Items.Count != 0)
                    {
                        failure = InventoryFailures.Snapshot("Snapshot object value contains incompatible payload data.");
                        return false;
                    }
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var property in value.Properties)
                    {
                        if (property == null || string.IsNullOrWhiteSpace(property.Name))
                        {
                            failure = InventoryFailures.Snapshot("Snapshot object property names cannot be null or empty.");
                            return false;
                        }
                        if (!names.Add(property.Name))
                        {
                            failure = InventoryFailures.Snapshot($"Snapshot object property '{property.Name}' is duplicated.");
                            return false;
                        }
                        if (!TryCloneEncoded(property.Value, path, out var propertyClone, out failure) || propertyClone == null)
                            return false;
                        clone.Properties.Add(new SnapshotNamedValue { Name = property.Name, Value = propertyClone });
                    }
                    break;
                default:
                    failure = InventoryFailures.Snapshot($"Snapshot value kind '{value.Kind}' is unsupported.");
                    return false;
            }

            failure = null;
            return true;
        }
        finally
        {
            path.Remove(value);
        }
    }

    private static bool TryCloneEncoded(
        SnapshotEncodedValue? value,
        HashSet<object> path,
        out SnapshotEncodedValue? clone,
        out InventoryFailure? failure)
    {
        clone = null;
        if (value == null ||
            string.IsNullOrWhiteSpace(value.CodecId) ||
            value.CodecVersion <= 0)
        {
            failure = InventoryFailures.Snapshot("Encoded snapshot values require a codec id and positive version.");
            return false;
        }
        if (!path.Add(value))
        {
            failure = InventoryFailures.Snapshot("Encoded snapshot value graph contains a cycle.");
            return false;
        }
        try
        {
            if (!TryClone(value.Data, path, out var data, out failure) || data == null)
                return false;
            clone = new SnapshotEncodedValue
            {
                CodecId = value.CodecId,
                CodecVersion = value.CodecVersion,
                Data = data
            };
            return true;
        }
        finally
        {
            path.Remove(value);
        }
    }
}

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new();

    private ReferenceEqualityComparer()
    {
    }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
