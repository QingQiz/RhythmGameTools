using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MaiCoverDownloader;

internal static class Program
{
    private const string Output = @"E:\MarisaBot\Marisa.Frontend\public\assets\maimai";
    private const string CoverUrl = "https://maimai.diving-fish.com/covers";
    private const string LxnsSongListUrl = "https://maimai.lxns.net/api/v0/maimai/song/list";
    private const string LxnsCoverUrl = "https://assets2.lxns.net/maimai/jacket";
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private sealed record SongInfo(int Id, string Title, string Artist, double Bpm);

    private sealed record LxnsSongInfo(int Id, string Title, string Artist, double Bpm);

    private static bool IsValidPng(string path)
    {
        if (!File.Exists(path)) return false;

        using var stream = File.OpenRead(path);
        if (stream.Length < PngSignature.Length) return false;

        Span<byte> header = stackalloc byte[PngSignature.Length];
        return stream.Read(header) == PngSignature.Length && header.SequenceEqual(PngSignature);
    }

    private static void DeleteIfInvalidPng(string path)
    {
        if (File.Exists(path) && !IsValidPng(path)) File.Delete(path);
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static string BuildSongKey(string title, string artist)
    {
        return $"{NormalizeText(title)}|{NormalizeText(artist)}";
    }

    private static double ParseBpm(JToken? token)
    {
        return token?.Value<double?>() ?? 0;
    }

    private static SongInfo ParseDivingFishSong(dynamic song)
    {
        return new SongInfo(
            int.Parse(song.id.ToString()),
            song.title?.ToString() ?? string.Empty,
            song.basic_info?.artist?.ToString() ?? string.Empty,
            ParseBpm(song.basic_info?.bpm));
    }

    private static List<LxnsSongInfo> ParseLxnsSongs(string text)
    {
        var root = JObject.Parse(text);
        var songs = root["songs"] as JArray ?? [];

        return songs
            .Select(song => new LxnsSongInfo(
                song?["id"]?.Value<int>() ?? 0,
                song?["title"]?.ToString() ?? string.Empty,
                song?["artist"]?.ToString() ?? string.Empty,
                ParseBpm(song?["bpm"])))
            .Where(song => song.Id > 0)
            .ToList();
    }

    private static int? FindLxnsSongId(SongInfo song, IReadOnlyDictionary<string, List<LxnsSongInfo>> songsByKey)
    {
        if (songsByKey.TryGetValue(BuildSongKey(song.Title, song.Artist), out var exactMatches))
        {
            if (exactMatches.Count == 1) return exactMatches[0].Id;

            var bpmMatch = exactMatches.FirstOrDefault(candidate => Math.Abs(candidate.Bpm - song.Bpm) < 0.001);
            if (bpmMatch is not null) return bpmMatch.Id;
        }

        return null;
    }

    private static async Task<bool> Download(string filename, int urlSuffix)
    {
        return await Download(filename, $"{CoverUrl}/{urlSuffix}.png");
    }

    private static async Task<bool> Download(string filename, string url)
    {
        var coverPath = Path.Join(Output, "cover");
        Directory.CreateDirectory(coverPath);

        var targetPath = Path.Join(coverPath, filename);
        var sourcePath = Path.Join(coverPath, Path.GetFileName(new Uri(url).AbsolutePath));

        DeleteIfInvalidPng(targetPath);
        if (IsValidPng(targetPath)) return true;

        DeleteIfInvalidPng(sourcePath);
        if (IsValidPng(sourcePath))
        {
            File.Copy(sourcePath, targetPath, true);
            return true;
        }

        try
        {
            await url
                .DownloadFileAsync(coverPath, filename);

            DeleteIfInvalidPng(targetPath);
            return IsValidPng(targetPath);
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            return false;
        }
    }

    private static List<int> GetAlternateIds(int songId)
    {
        var res = new List<int>
        {
            songId
        };

        switch (songId)
        {
            case < 10000:
                res.Add(songId + 10000);
                res.Add(songId + 100000);
                break;
            case < 100000:
                res.Add(songId - 10000);
                res.Add(songId + 100000);
                break;
            default:
            {
                for (var i = 100000;; i += 10000)
                {
                    if (songId < i) break;
                    res.Add(songId - i);
                }
                break;
            }
        }
        return res;
    }

    private static void L(List<int> x)
    {
        Console.WriteLine(string.Join(", ", x));
    }

    private static async Task Download4Song(SongInfo song, IReadOnlyDictionary<string, List<LxnsSongInfo>> lxnsSongsByKey)
    {
        foreach (var alt in GetAlternateIds(song.Id))
        {
            if (!await Download($"{song.Id}.png", alt)) continue;
            return;
        }

        var lxnsSongId = FindLxnsSongId(song, lxnsSongsByKey);
        if (lxnsSongId is not null && await Download($"{song.Id}.png", $"{LxnsCoverUrl}/{lxnsSongId}.png")) return;

        Console.WriteLine($"Cover Not Found: {song.Id} - {song.Title}");
    }

    public static async Task Main()
    {
        var text = await "https://www.diving-fish.com/api/maimaidxprober/music_data".GetStringAsync();
        var lxnsText = await LxnsSongListUrl.GetStringAsync();
        var rep  = JsonConvert.DeserializeObject<dynamic[]>(text);
        var lxnsSongsByKey = ParseLxnsSongs(lxnsText)
            .GroupBy(song => BuildSongKey(song.Title, song.Artist))
            .ToDictionary(group => group.Key, group => group.ToList());

        var w   = File.WriteAllTextAsync(Path.Join(Output, "SongInfo.json"), JsonConvert.SerializeObject(rep, Formatting.Indented));
        var songs = rep!.Select(ParseDivingFishSong).ToList();

        await Task.WhenAll(songs.Select(song => Download4Song(song, lxnsSongsByKey)));
        await w;
    }
}
