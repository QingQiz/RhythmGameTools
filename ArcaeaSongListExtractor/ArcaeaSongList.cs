﻿// Generated by https://quicktype.io

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ArcaeaSongListExtractor;

public partial class ArcaeaSongList
{
    [JsonProperty("songs")]
    public Song[] Songs { get; set; }
}

public partial class Song
{
    [JsonProperty("idx")]
    public long Idx { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("title_localized")]
    public SongTitleLocalized TitleLocalized { get; set; }

    [JsonProperty("artist")]
    public string Artist { get; set; }

    [JsonProperty("search_title", NullValueHandling = NullValueHandling.Ignore)]
    public Search SearchTitle { get; set; }

    [JsonProperty("search_artist")]
    public Search SearchArtist { get; set; }

    [JsonProperty("bpm")]
    public string Bpm { get; set; }

    [JsonProperty("bpm_base")]
    public double BpmBase { get; set; }

    [JsonProperty("set")]
    public string Set { get; set; }

    [JsonProperty("purchase")]
    public string Purchase { get; set; }

    [JsonProperty("audioPreview")]
    public long AudioPreview { get; set; }

    [JsonProperty("audioPreviewEnd")]
    public long AudioPreviewEnd { get; set; }

    [JsonProperty("side")]
    public long Side { get; set; }

    [JsonProperty("bg")]
    public string Bg { get; set; }

    [JsonProperty("bg_inverse", NullValueHandling = NullValueHandling.Ignore)]
    public string BgInverse { get; set; }

    [JsonProperty("date")]
    public long Date { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("difficulties")]
    public Difficulty[] Difficulties { get; set; }

    [JsonProperty("world_unlock", NullValueHandling = NullValueHandling.Ignore)]
    public bool? WorldUnlock { get; set; }

    [JsonProperty("remote_dl", NullValueHandling = NullValueHandling.Ignore)]
    public bool? RemoteDl { get; set; }

    [JsonProperty("source_localized", NullValueHandling = NullValueHandling.Ignore)]
    public SourceLocalized SourceLocalized { get; set; }

    [JsonProperty("source_copyright", NullValueHandling = NullValueHandling.Ignore)]
    public string SourceCopyright { get; set; }

    [JsonProperty("no_stream", NullValueHandling = NullValueHandling.Ignore)]
    public bool? NoStream { get; set; }

    [JsonProperty("jacket_localized", NullValueHandling = NullValueHandling.Ignore)]
    public JacketLocalized JacketLocalized { get; set; }

    [JsonProperty("bg_daynight", NullValueHandling = NullValueHandling.Ignore)]
    public BgDaynight BgDaynight { get; set; }

    [JsonProperty("byd_local_unlock", NullValueHandling = NullValueHandling.Ignore)]
    public bool? BydLocalUnlock { get; set; }

    [JsonProperty("additional_files", NullValueHandling = NullValueHandling.Ignore)]
    public AdditionalFile[] AdditionalFiles { get; set; }

    [JsonProperty("songlist_hidden", NullValueHandling = NullValueHandling.Ignore)]
    public bool? SonglistHidden { get; set; }
}

public partial class AdditionalFile
{
    [JsonProperty("file_name")]
    public string FileName { get; set; }

    [JsonProperty("requirement")]
    public string Requirement { get; set; }
}

public partial class BgDaynight
{
    [JsonProperty("day")]
    public string Day { get; set; }

    [JsonProperty("night")]
    public string Night { get; set; }
}

public partial class Difficulty
{
    [JsonProperty("ratingClass")]
    public long RatingClass { get; set; }

    [JsonProperty("chartDesigner")]
    public string ChartDesigner { get; set; }

    [JsonProperty("jacketDesigner")]
    public string JacketDesigner { get; set; }

    [JsonProperty("rating")]
    public long Rating { get; set; }

    [JsonProperty("jacketOverride", NullValueHandling = NullValueHandling.Ignore)]
    public bool? JacketOverride { get; set; }

    [JsonProperty("ratingPlus", NullValueHandling = NullValueHandling.Ignore)]
    public bool? RatingPlus { get; set; }

    [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
    public long? Date { get; set; }

    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public string Version { get; set; }

    [JsonProperty("title_localized", NullValueHandling = NullValueHandling.Ignore)]
    public DifficultyTitleLocalized TitleLocalized { get; set; }

    [JsonProperty("audioOverride", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AudioOverride { get; set; }

    [JsonProperty("bg", NullValueHandling = NullValueHandling.Ignore)]
    public string Bg { get; set; }

    [JsonProperty("plusFingers", NullValueHandling = NullValueHandling.Ignore)]
    public bool? PlusFingers { get; set; }

    [JsonProperty("artist", NullValueHandling = NullValueHandling.Ignore)]
    public string Artist { get; set; }

    [JsonProperty("bg_inverse", NullValueHandling = NullValueHandling.Ignore)]
    public string BgInverse { get; set; }

    [JsonProperty("bpm", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(ParseStringConverter))]
    public long? Bpm { get; set; }

    [JsonProperty("bpm_base", NullValueHandling = NullValueHandling.Ignore)]
    public long? BpmBase { get; set; }

    [JsonProperty("jacket_night", NullValueHandling = NullValueHandling.Ignore)]
    public string JacketNight { get; set; }

    [JsonProperty("hidden_until_unlocked", NullValueHandling = NullValueHandling.Ignore)]
    public bool? HiddenUntilUnlocked { get; set; }

    [JsonProperty("hidden_until", NullValueHandling = NullValueHandling.Ignore)]
    public string HiddenUntil { get; set; }

    [JsonProperty("world_unlock", NullValueHandling = NullValueHandling.Ignore)]
    public bool? WorldUnlock { get; set; }
}

public partial class DifficultyTitleLocalized
{
    [JsonProperty("en")]
    public string En { get; set; }
}

public partial class JacketLocalized
{
    [JsonProperty("ja")]
    public bool Ja { get; set; }
}

public partial class Search
{
    [JsonProperty("ja", NullValueHandling = NullValueHandling.Ignore)]
    public string[] Ja { get; set; }

    [JsonProperty("ko")]
    public string[] Ko { get; set; }

    [JsonProperty("en", NullValueHandling = NullValueHandling.Ignore)]
    public string[] En { get; set; }
}

public partial class SourceLocalized
{
    [JsonProperty("en")]
    public string En { get; set; }

    [JsonProperty("ja", NullValueHandling = NullValueHandling.Ignore)]
    public string Ja { get; set; }
}

public partial class SongTitleLocalized
{
    [JsonProperty("en")]
    public string En { get; set; }

    [JsonProperty("ko", NullValueHandling = NullValueHandling.Ignore)]
    public string Ko { get; set; }

    [JsonProperty("zh-Hant", NullValueHandling = NullValueHandling.Ignore)]
    public string ZhHant { get; set; }

    [JsonProperty("zh-Hans", NullValueHandling = NullValueHandling.Ignore)]
    public string ZhHans { get; set; }

    [JsonProperty("ja", NullValueHandling = NullValueHandling.Ignore)]
    public string Ja { get; set; }

    [JsonProperty("kr", NullValueHandling = NullValueHandling.Ignore)]
    public string Kr { get; set; }
}

public partial class ArcaeaSongList
{
    public static ArcaeaSongList FromJson(string json) =>
        JsonConvert.DeserializeObject<ArcaeaSongList>(json, Converter.Settings);
}

public static class Serialize
{
    public static string ToJson(this ArcaeaSongList self) => JsonConvert.SerializeObject(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling        = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.AssumeUniversal}
        },
    };
}

internal class ParseStringConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var  value = serializer.Deserialize<string>(reader);
        long l;
        if (Int64.TryParse(value, out l))
        {
            return l;
        }

        throw new Exception("Cannot unmarshal type long");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (long) untypedValue;
        serializer.Serialize(writer, value.ToString());
        return;
    }

    public static readonly ParseStringConverter Singleton = new ParseStringConverter();
}