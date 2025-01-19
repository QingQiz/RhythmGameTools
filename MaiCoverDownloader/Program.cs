using Flurl.Http;
using Newtonsoft.Json;

namespace MaiCoverDownloader;

internal static class Program
{
    private const string Output = @"C:\Users\sofee\Desktop\workspace\QQBOT\Marisa.Frontend\public\assets\maimai";

    private static async Task<bool> Download(string filename, int urlSuffix)
    {
        var coverPath = Path.Join(Output, "cover");
        if (File.Exists(Path.Join(coverPath, filename))) return true;
        if (File.Exists(Path.Join(coverPath, $"{urlSuffix}.png")))
        {
            File.Copy(Path.Join(coverPath, $"{urlSuffix}.png"), Path.Join(coverPath, filename));
            return true;
        }

        try
        {
            await $"https://assets.lxns.net/maimai/jacket/{urlSuffix.ToString()}.png"
                .DownloadFileAsync(coverPath, filename);
            return true;
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

    private static async Task Download4Id(int id)
    {
        foreach (var alt in GetAlternateIds(id))
        {
            if (!await Download($"{id}.png", alt)) continue;
            return;
        }
        Console.WriteLine($"Cover Not Found: {id}");
    }

    public static async Task Main()
    {
        var text = await "https://www.diving-fish.com/api/maimaidxprober/music_data".GetStringAsync();
        var rep  = JsonConvert.DeserializeObject<dynamic[]>(text);

        var w   = File.WriteAllTextAsync(Path.Join(Output, "SongInfo.json"), JsonConvert.SerializeObject(rep, Formatting.Indented));
        var ids = rep!.Select(i => int.Parse(i.id.ToString())).Cast<int>().ToList();

        await Task.WhenAll(ids.Select(Download4Id));
        await w;
    }
}