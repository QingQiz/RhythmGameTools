using System.Dynamic;
using Flurl.Http;

namespace MaiCoverDownloader;

internal static class Program
{
    private const string Output = @"C:\Users\sofee\Desktop\workspace\QQBOT\Marisa.Frontend\public\assets\maimai\cover";

    private static async Task<bool> Download(string filename, int urlSuffix)
    {
        if (File.Exists(Path.Join(Output, filename))) return true;
        if (File.Exists(Path.Join(Output, $"{urlSuffix}.png")))
        {
            File.Copy(Path.Join(Output + $"{urlSuffix}.png"), Path.Join(Output, filename));
            return true;
        }

        try
        {
            await $"https://www.diving-fish.com/covers/{urlSuffix.ToString().PadLeft(5, '0')}.png"
                .DownloadFileAsync(Output, filename);
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
            if (!await Download($"{alt}.png", alt)) continue;
            return;
        }
        Console.WriteLine($"Cover Not Found: {id}");
    }

    public static async Task Main(string[] args)
    {
        var rep = await "https://www.diving-fish.com/api/maimaidxprober/music_data"
            .GetJsonAsync<ExpandoObject[]>() as dynamic[];

        var ids = rep.Select(i => int.Parse(i.id.ToString())).Cast<int>().ToList();

        await Task.WhenAll(ids.Select(Download4Id));
    }
}