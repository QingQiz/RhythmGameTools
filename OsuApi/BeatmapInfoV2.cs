using Newtonsoft.Json;

namespace OsuApi;

#pragma warning disable CS8618
/**
 * osu! API v1 Beatmap Info
 * see https://osu.ppy.sh/docs/index.html#beatmapsetextended
 */
public class BeatmapInfoV2 : BeatmapInfoBase
{

    [JsonProperty("difficulty_rating")]
    public double DifficultyRating { get; set; }

    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("mode")]
    public string Mode { get; set; } // Could be enum if known values

    [JsonProperty("status")]
    public string Status { get; set; } // e.g., "ranked", "approved", etc.

    [JsonProperty("total_length")]
    public int TotalLength { get; set; } // in seconds

    [JsonProperty("user_id")]
    public long UserId { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; } // difficulty name

    [JsonProperty("accuracy")]
    public double Accuracy { get; set; }

    [JsonProperty("ar")]
    public double ApproachRate { get; set; } // AR value

    // Note: Duplicate beatmapset_id field in source, only include once
    // [JsonProperty("beatmapset_id")] 

    [JsonProperty("bpm")]
    public double? Bpm { get; set; } // nullable

    [JsonProperty("convert")]
    public bool IsConvert { get; set; } // convert from another mode

    [JsonProperty("count_circles")]
    public int CircleCount { get; set; }

    [JsonProperty("count_sliders")]
    public int SliderCount { get; set; }

    [JsonProperty("count_spinners")]
    public int SpinnerCount { get; set; }

    [JsonProperty("cs")]
    public double CircleSize { get; set; } // CS value

    [JsonProperty("deleted_at")]
    public DateTime? DeletedAt { get; set; } // nullable timestamp

    [JsonProperty("drain")]
    public double Drain { get; set; } // HP drain value

    [JsonProperty("hit_length")]
    public int HitLength { get; set; } // in seconds (active gameplay)

    [JsonProperty("is_scoreable")]
    public bool IsScoreable { get; set; }

    [JsonProperty("last_updated")]
    public DateTime LastUpdated { get; set; } // timestamp

    [JsonProperty("mode_int")]
    public int ModeInt { get; set; } // integer representation of mode

    [JsonProperty("passcount")]
    public int PassCount { get; set; } // successful completions

    [JsonProperty("playcount")]
    public int PlayCount { get; set; } // total plays

    [JsonProperty("ranked")]
    public int RankedStatus { get; set; } // numeric rank status

    [JsonProperty("url")]
    public string Url { get; set; } // beatmap URL
}