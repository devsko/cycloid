using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cycloid.Info;

public class OverpassEnumConverter<T> : JsonConverter<T> where T: struct, Enum
{
    private readonly Dictionary<string, T> _knownValues = CreateKnownValues();

    private static Dictionary<string, T> CreateKnownValues()
    {
        Dictionary<string, T> knownValues = [];

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();

        for (int i = 0; i < names.Length; i++)
        {
            knownValues.Add(names[i], values[i]);
        }

        return knownValues;
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_knownValues.TryGetValue(reader.GetString() ?? string.Empty, out T value))
        {
            return value;
        }

        int unknown = -1;
        return Unsafe.As<int, T>(ref unknown);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}