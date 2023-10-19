#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
/// auto generated

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace QuickType;

public partial class ChunithmMusicData
{
    [JsonProperty("MusicData", Required = Required.Always)]
    public MusicData Data { get; set; }
}

public partial class MusicData
{
    [JsonProperty("@xmlns:xsd", Required = Required.Always)]
    public Uri XmlnsXsd { get; set; }

    [JsonProperty("@xmlns:xsi", Required = Required.Always)]
    public Uri XmlnsXsi { get; set; }

    [JsonProperty("dataName", Required = Required.Always)]
    public string DataName { get; set; }

    [JsonProperty("releaseTagName", Required = Required.Always)]
    public ArtistName ReleaseTagName { get; set; }

    [JsonProperty("netOpenName", Required = Required.Always)]
    public ArtistName NetOpenName { get; set; }

    [JsonProperty("disableFlag")]
    [JsonConverter(typeof(FluffyParseStringConverter))]
    public bool DisableFlag { get; set; } = false;

    [JsonProperty("exType", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long ExType { get; set; }

    [JsonProperty("name", Required = Required.Always)]
    public ArtistName Name { get; set; }

    [JsonProperty("sortName", Required = Required.Always)]
    public string SortName { get; set; }

    [JsonProperty("artistName", Required = Required.Always)]
    public ArtistName ArtistName { get; set; }

    [JsonProperty("genreNames", Required = Required.Always)]
    public GenreNames GenreNames { get; set; }

    [JsonProperty("worksName")]
    public ArtistName WorksName { get; set; }

    [JsonProperty("jaketFile", Required = Required.Always)]
    public File JaketFile { get; set; }

    [JsonProperty("firstLock", Required = Required.Always)]
    [JsonConverter(typeof(FluffyParseStringConverter))]
    public bool FirstLock { get; set; }

    [JsonProperty("enableUltima", Required = Required.Always)]
    [JsonConverter(typeof(FluffyParseStringConverter))]
    public bool EnableUltima { get; set; }

    [JsonProperty("priority", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long Priority { get; set; }

    [JsonProperty("cueFileName", Required = Required.Always)]
    public ArtistName CueFileName { get; set; }

    [JsonProperty("worldsEndTagName", Required = Required.Always)]
    public ArtistName WorldsEndTagName { get; set; }

    [JsonProperty("starDifType", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long StarDifType { get; set; }

    [JsonProperty("stageName", Required = Required.Always)]
    public ArtistName StageName { get; set; }

    [JsonProperty("fumens", Required = Required.Always)]
    public Fumens Fumens { get; set; }
}

public partial class ArtistName
{
    [JsonProperty("id", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long Id { get; set; }

    [JsonProperty("str", Required = Required.Always)]
    public string Str { get; set; }

    [JsonProperty("data", Required = Required.AllowNull)]
    public string Data { get; set; }
}

public partial class Fumens
{
    [JsonProperty("MusicFumenData", Required = Required.Always)]
    public MusicFumenDatum[] MusicFumenData { get; set; }
}

public partial class MusicFumenDatum
{
    [JsonProperty("type", Required = Required.Always)]
    public ArtistName Type { get; set; }

    [JsonProperty("enable", Required = Required.Always)]
    [JsonConverter(typeof(FluffyParseStringConverter))]
    public bool Enable { get; set; }

    [JsonProperty("file", Required = Required.Always)]
    public File File { get; set; }

    [JsonProperty("level", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long Level { get; set; }

    [JsonProperty("levelDecimal", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long LevelDecimal { get; set; }

    [JsonProperty("notesDesigner", Required = Required.AllowNull)]
    public object NotesDesigner { get; set; }

    [JsonProperty("defaultBpm", Required = Required.Always)]
    [JsonConverter(typeof(PurpleParseStringConverter))]
    public long DefaultBpm { get; set; }
}

public partial class File
{
    [JsonProperty("path", Required = Required.AllowNull)]
    public string Path { get; set; }
}

public partial class GenreNames
{
    [JsonProperty("list", Required = Required.Always)]
    public List List { get; set; }
}

public partial class List
{
    [JsonProperty("StringID", Required = Required.Always)]
    public ArtistName StringId { get; set; }
}

public partial class ChunithmMusicData
{
    public static ChunithmMusicData FromJson(string json)
    {
        return JsonConvert.DeserializeObject<ChunithmMusicData>(json, Converter.Settings);
    }
}

public static class Serialize
{
    public static string ToJson(this ChunithmMusicData self)
    {
        return JsonConvert.SerializeObject(self, Converter.Settings);
    }
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        }
    };
}

internal class PurpleParseStringConverter : JsonConverter
{
    public override bool CanConvert(Type t)
    {
        return t == typeof(long) || t == typeof(long?);
    }

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = serializer.Deserialize<string>(reader);
        long l;
        if (long.TryParse(value, out l)) return l;
        throw new Exception("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (long)untypedValue;
        serializer.Serialize(writer, value.ToString());
        return;
    }

    public static readonly PurpleParseStringConverter Singleton = new();
}

internal class FluffyParseStringConverter : JsonConverter
{
    public override bool CanConvert(Type t)
    {
        return t == typeof(bool) || t == typeof(bool?);
    }

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = serializer.Deserialize<string>(reader);
        bool b;
        if (bool.TryParse(value, out b)) return b;
        throw new Exception("Cannot unmarshal type bool");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (bool)untypedValue;
        var boolString = value ? "true" : "false";
        serializer.Serialize(writer, boolString);
        return;
    }

    public static readonly FluffyParseStringConverter Singleton = new();
}
