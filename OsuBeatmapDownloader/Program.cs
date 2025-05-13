using System.Collections.Concurrent;
using Flurl.Http;
using Newtonsoft.Json;
using OsuApi;

namespace OsuBeatmapDownloader;

internal static class Program
{
    private const string LazerPath = @"O:\GameStorage\osu!lazer";
    private const string Output = LazerPath;

    private static readonly string[] RecommendMapperList =
    [
        // 7k 鱼丸推荐
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
        "tyrcs", "ruka", "Leeju",
        // 6k 曲包的谱师
        "QQwiwi2012", "Arkman", "[Crz]Derrick", "doubu", "_IceRain", "[Crz]Emperor", "Alipay", "HMillion", "Benson_",
        "[Crz]sunnyxxy", "BKwind", "tyrcs",
        // 7k 自己找的
        "[Crz]Emperor-", "richardfeder", "soulseason", "MEIDAN", "shiyu1213", "Aaki_", "ApoLar", "YyottaCat", "7777",
        "_Yiiiii"
    ];


    private static async Task<List<BeatmapSet>> GetRecommendMapList()
    {
        var cacheName = DateTime.Now.ToString("[yyyy-MM-dd]") + "beatmaps.json";

        if (File.Exists(cacheName))
        {
            // deserialize
            return JsonConvert.DeserializeObject<List<BeatmapSet>>(await File.ReadAllTextAsync(cacheName))!;
        }

        var mapList = new List<BeatmapSet>();

        Parallel.ForEach(RecommendMapperList.Distinct(), new ParallelOptions { MaxDegreeOfParallelism = 8 }, user =>
        {
            RETRY:
            try
            {
                Console.WriteLine("Requesting " + user);
                var res = OsuWebApi.GetUserBeatmapSets(user).Result;
                lock (mapList)
                {
                    mapList.AddRange(res);
                }
            }
            catch (FlurlHttpException e) when (e.StatusCode == 404)
            {
                Console.WriteLine($"User {user} not found.");
            }
            catch (AggregateException e) when (e.InnerExceptions.All(x => (x as FlurlHttpException)?.StatusCode == 404))
            {
                Console.WriteLine($"User {user} not found.");
            }
            catch
            {
                Console.WriteLine("RETRY Requesting " + user);
                goto RETRY;
            }
        });

        // write to file
        await File.WriteAllTextAsync(cacheName, JsonConvert.SerializeObject(mapList));

        return mapList;
    }

    private static List<BeatmapSet> RemoveDownloadedBeatmaps(IList<BeatmapSet> mapList)
    {
        return LazerDbApi.WithAllBeatmapSetInfo(LazerPath, list =>
        {
            var downloaded = list.Select(x => x.OnlineID).ToHashSet();
            return mapList
                .DistinctBy(x => x.Id)
                .Where(x => !downloaded.Contains(x.Id))
                .ToList();
        });
    }

    private static int _cnt;
    private static readonly object CntLk = new();

    private static void DownloadTask(ConcurrentQueue<(BeatmapSet Map, int Count)> queue, int maxRetry)
    {
        while (queue.TryDequeue(out var map))
        {
            try
            {
                OsuWebApi.DownloadBeatmap(map.Map.Id, Output).GetAwaiter().GetResult();
                Console.WriteLine($"Downloaded {map.Map.Id} {map.Map.Title} - {map.Map.Artist} by {map.Map.Creator}");
                lock (CntLk) _cnt--;
            }
            catch (Exception e)
            {
                if (map.Count < maxRetry)
                {
                    Console.WriteLine($"Download {map.Map.Id} failed: {e.Message}. RETRYING... ({map.Count + 1}/{maxRetry}");
                    queue.Enqueue((map.Map, map.Count + 1));
                }
                else
                {
                    Console.WriteLine($"Download {map.Map.Id} failed: {e.Message}. GIVE UP.");
                    lock (CntLk) _cnt--;
                }
            }
            finally
            {
                Console.Write($"Remaining {_cnt} beatmaps ");
            }
        }
    }

    private static async Task Main()
    {
        if (!Directory.Exists(Output))
        {
            Directory.CreateDirectory(Output);
        }

        var recommendMapList = await GetRecommendMapList();
        var rankedMapList    = await OsuWebApi.GetRankedBeatmapSets();

        var mapList = rankedMapList.Concat(recommendMapList).DistinctBy(x => x.Id).ToList();
        mapList = RemoveDownloadedBeatmaps(mapList);

        Console.WriteLine($"Downloading {mapList.Count} beatmaps");
        _cnt = mapList.Count;

        // create thread pool
        var queue = new ConcurrentQueue<(BeatmapSet Map, int Count)>(mapList.Select(x => (x, 0)));

        // start download
        var tasks = new Task[Environment.ProcessorCount];

        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => DownloadTask(queue, 10));
        }

        await Task.WhenAll(tasks);
    }
}