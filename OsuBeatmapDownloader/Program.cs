using Flurl.Http;
using Newtonsoft.Json;

namespace OsuBeatmapDownloader;

internal record BeatmapSet(int Id, string Title, string Artist, string Creator, string Status);

internal static class Program
{
    private static async Task<List<BeatmapSet>> GetUserBeatmapSets(string username)
    {
        var cacheName = DateTime.Now.ToString("[yyyy-MM-dd]") + $"[{username}].json";
        if (File.Exists(cacheName))
        {
            // deserialize
            return JsonConvert.DeserializeObject<List<BeatmapSet>>(await File.ReadAllTextAsync(cacheName));
        }

        var user = await OsuApi.Request(OsuApi.ApiRoot + $"/users/{username}/mania?key=username").GetJsonAsync();

        var type = new[]
        {
            "graveyard",
            "guest",
            "loved",
            "ranked"
        };

        var res = new List<BeatmapSet>();

        foreach (var t in type)
        {
            const int limit  = 50;
            var       offset = 0;

            while (true)
            {
                var j = await OsuApi
                    .Request(OsuApi.ApiRoot + $"/users/{user.id}/beatmapsets/{t}?limit={limit}&offset={offset}")
                    .GetJsonListAsync();

                if (!j.Any()) break;

                res.AddRange(j.Select(x =>
                    new BeatmapSet((int)x.id, (string)x.title, (string)x.artist, (string)x.creator, t))
                );

                offset += limit;
            }
        }

        await File.WriteAllTextAsync(cacheName, JsonConvert.SerializeObject(res));

        return res;
    }

    private static async Task<List<BeatmapSet>> GetRecommendMapList()
    {
        if (File.Exists("beatmapsets.json"))
        {
            // deserialize
            return JsonConvert.DeserializeObject<List<BeatmapSet>>(await File.ReadAllTextAsync("beatmapsets.json"));
        }

        var userList = new[]
        {
            //
            "Evening", "Kim_GodSSI", "Jinjin", "_underjoy", "Wonki", "taba2", "Hehoooh", "paulkappa", "Remuring",
            "paradoxus_",
            "_Kobii", "Blocko", "ExNeko", "Alsty-", "Schopfer", "Tropicar", "_Stan", "erased self", "-NoName-",
            "TakJun",
            "Imperial Wolf", "AncuL",
            //
            "Muses", "quicalid4", "_Reimu", "m1n530k", "Laply", "Lung_P", "Emida", "yellEx", "Chyo_N", "Dety",
            "emO_Oticon",
            "Nananana", "My Angel Koishi", "arcwinolivirus", "MapleSyrup-", "Enie", "Akayro", "Arona", "kasumi99",
            "Mage",
            "JDS20", "GoosBaams", "AWMRone",
            //
            "sankansuki", "lenpai", "Flexo123", "qodtjr", "tangjinxi", "Kawawa", "17VA", "Reba", "Pengdoll", "LostCool",
            "Critical_Star", "Rurikon_", "Wilben_Chan", "- Minato Aqua -", "Entozer", "Cuppp", "pwhk", "Nivrad00",
            //
            "tyrcs", "ruka", "Leeju"
        };

        var mapList = new List<BeatmapSet>();

        foreach (var user in userList)
        {
            try
            {
                mapList.AddRange(await GetUserBeatmapSets(user));
            }
            catch (FlurlHttpException e) when (e.StatusCode == 404)
            {
                Console.WriteLine($"User {user} not found.");
            }
        }

        // write to file
        await File.WriteAllTextAsync("beatmapsets.json", JsonConvert.SerializeObject(mapList));

        return mapList;
    }

    private static List<BeatmapSet> RemoveDownloadedBeatmaps(IList<BeatmapSet> mapList)
    {
        var set = Directory
            .GetFiles(Output, "*.osz", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetDirectories(Output, "*", SearchOption.TopDirectoryOnly))
            .Select(Path.GetFileName)
            .Select(x => x!.Split(' ', 2)[0])
            .Where(x => int.TryParse(x, out _))
            .Select(int.Parse)
            .Where(x => x > 10)
            .ToHashSet();

        return mapList
            .DistinctBy(x => x.Id)
            .Where(x => !set.Contains(x.Id))
            .ToList();
    }

    private const string Output = @"O:\GameStorage\osu!\Songs";

    private static async Task Main(string[] args)
    {
        var mapList = await GetRecommendMapList();

        mapList = RemoveDownloadedBeatmaps(mapList);

        Console.WriteLine($"Downloading {mapList.Count} beatmaps");
        
        Parallel.ForEach(mapList, map =>
        {
            try
            {
                OsuApi.DownloadBeatmap(map.Id, Output).GetAwaiter().GetResult();
                Console.WriteLine($"Downloaded {map.Id} {map.Title} - {map.Artist} by {map.Creator}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download {map.Id} failed: {e.Message}");
            }
        });
    }
}
