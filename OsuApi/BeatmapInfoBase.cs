using Newtonsoft.Json;

namespace OsuApi;

#pragma warning disable CS8618
public class BeatmapInfoBase
{
    [JsonProperty("beatmapset_id")]
    public int BeatmapSetId { get; set; }

    [JsonProperty("artist")]
    public string Artist { get; set; }

    [JsonProperty("creator")]
    public string Creator { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }
}