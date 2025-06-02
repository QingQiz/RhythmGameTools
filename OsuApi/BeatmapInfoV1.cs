using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OsuApi;

#pragma warning disable CS8618
/**
 * osu! API v1 Beatmap Info
 * see https://github.com/ppy/osu-api/wiki#response
 */
public class BeatmapInfoV1 : BeatmapInfoBase
{
    [JsonProperty("approved")]
    public int Approved { get; set; } // -2:graveyard, -1:WIP, 0:pending, 1:ranked, 2:approved, 3:qualified, 4:loved

    [JsonProperty("submit_date")]
    public DateTime SubmitDate { get; set; } // UTC

    [JsonProperty("approved_date")]
    public string ApprovedDate { get; set; } // UTC (nullable for unranked maps)

    [JsonProperty("last_update")]
    public DateTime LastUpdate { get; set; } // UTC

    [JsonProperty("beatmap_id")]
    public long BeatmapId { get; set; }

    [JsonProperty("bpm")]
    public double Bpm { get; set; }

    [JsonProperty("creator_id")]
    public long CreatorId { get; set; }

    [JsonProperty("difficultyrating")]
    public double StarRating { get; set; }

    [JsonProperty("diff_aim")]
    public double? AimDifficulty { get; set; }

    [JsonProperty("diff_speed")]
    public double? SpeedDifficulty { get; set; }

    [JsonProperty("diff_size")]
    public double CircleSize { get; set; } // CS

    [JsonProperty("diff_overall")]
    public double OverallDifficulty { get; set; } // OD

    [JsonProperty("diff_approach")]
    public double ApproachRate { get; set; } // AR

    [JsonProperty("diff_drain")]
    public double HpDrain { get; set; } // HP

    [JsonProperty("hit_length")]
    public int HitLength { get; set; } // seconds (excluding breaks)

    [JsonProperty("source")]
    public string Source { get; set; }

    /**
     Genre:
         0: any, 1: unspecified, 2: video game, 3: anime, 4: rock
         5: pop, 6: other, 7: novelty, 9: hip hop, 10: electronic
         11: metal, 12: classical, 13: folk, 14: jazz
     */
    [JsonProperty("genre_id")]
    public int GenreId { get; set; } // See genre mapping in comments

    /**
     Language:
         0: any, 1: unspecified, 2: english, 3: japanese, 4: chinese
         5: instrumental, 6: korean, 7: french, 8: german, 9: swedish
         10: spanish, 11: italian, 12: russian, 13: polish, 14: other
    */
    [JsonProperty("language_id")]
    public int LanguageId { get; set; } // See language mapping in comments

    [JsonProperty("total_length")]
    public int TotalLength { get; set; } // seconds (including breaks)

    [JsonProperty("version")]
    public string DifficultyName { get; set; }

    [JsonProperty("file_md5")]
    public string FileMd5 { get; set; }

    [JsonProperty("mode")]
    [JsonConverter(typeof(StringEnumConverter))]
    public GameMode Mode { get; set; } // Enum conversion

    [JsonProperty("tags")]
    public string Tags { get; set; }

    [JsonProperty("favourite_count")]
    public int FavouriteCount { get; set; }

    [JsonProperty("rating")]
    public double Rating { get; set; }

    [JsonProperty("playcount")]
    public int PlayCount { get; set; }

    [JsonProperty("passcount")]
    public int PassCount { get; set; }

    [JsonProperty("count_normal")]
    public int RiceCount { get; set; }

    [JsonProperty("count_slider")]
    public int SliderCount { get; set; }

    [JsonProperty("count_spinner")]
    public int SpinnerCount { get; set; }

    [JsonProperty("max_combo")]
    public int? MaxCombo { get; set; } // Nullable for unplayed maps
}

// Game Mode Enum (matches osu! API values)
public enum GameMode
{
    Osu = 0,
    Taiko = 1,
    Catch = 2,
    Mania = 3
}