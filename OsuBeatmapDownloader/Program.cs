using System.Collections.Concurrent;
using Flurl.Http;
using Newtonsoft.Json;
using OsuApi;

namespace OsuBeatmapDownloader;

internal static class Program
{
    private const string LazerPath = @"O:\GameStorage\osu!lazer";
    private static string Output => Path.Join(LazerPath, "download");

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


    private static async Task<List<BeatmapInfoV2>> GetRecommendMapList(Func<BeatmapInfoV2, bool>? filter = null)
    {
        var cacheName = DateTime.Now.ToString("[yyyy-MM-dd]") + "beatmaps.json";

        if (File.Exists(cacheName))
        {
            // deserialize
            var res = JsonConvert.DeserializeObject<List<BeatmapInfoV2>>(await File.ReadAllTextAsync(cacheName))!;
            return filter == null ? res : res.Where(filter).ToList();
        }

        var mapList = new List<BeatmapInfoV2>();

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

        return filter == null ? mapList : mapList.Where(filter).ToList();
    }

    private static List<BeatmapInfoBase> RemoveDownloadedBeatmaps(this List<BeatmapInfoBase> mapList)
    {
        return LazerDbApi.WithAllBeatmapSetInfo(LazerPath, list =>
        {
            var downloaded = list.Select(x => x.OnlineID).ToHashSet();
            var res = mapList
                .DistinctBy(x => x.BeatmapSetId)
                .Where(x => !downloaded.Contains(x.BeatmapSetId))
                .ToList();
            Console.WriteLine($"Removing {mapList.Count - res.Count} downloaded/duplicate beatmaps from {mapList.Count} total beatmaps.");
            return res;
        });
    }

    private static int _cnt;
    private static int _failed;
    private static readonly Lock CntLk = new();

    private static void DownloadTask(ConcurrentQueue<(BeatmapInfoBase Map, int Count)> queue, int maxRetry)
    {
        while (queue.TryDequeue(out var map))
        {
            try
            {
                OsuWebApi.DownloadBeatmap(map.Map.BeatmapSetId, Output).GetAwaiter().GetResult();
                Console.WriteLine($"Downloaded {map.Map.BeatmapSetId} {map.Map.Title} - {map.Map.Artist} by {map.Map.Creator}");
                lock (CntLk) _cnt--;
            }
            catch (Exception)
            {
                if (map.Count < maxRetry)
                {
                    Console.WriteLine($"Failed to download {map.Map.BeatmapSetId} {map.Map.Title} - {map.Map.Artist}. RETRYING... ({map.Count + 1}/{maxRetry})");
                    queue.Enqueue((map.Map, map.Count + 1));
                }
                else
                {
                    Console.WriteLine($"Failed to download {map.Map.BeatmapSetId} {map.Map.Title} - {map.Map.Artist}. GIVE UP.");
                    lock (CntLk)
                    {
                        _cnt--;
                        _failed++;
                    }
                }
            }
            finally
            {
                Console.Write($"Remaining {_cnt} beatmaps. ");
            }
        }
    }

    private static async Task Main()
    {
        // ensure output directory exists
        Directory.CreateDirectory(Output);

        // generate all beatmaps to download
        var toDownload = new[]
        {
            (await GetRecommendMapList()).Cast<BeatmapInfoBase>(),
            await OsuWebApi.GetRankedBeatmapSets(GameMode.Mania),
            await OsuWebApi.GetRankedBeatmapSets(GameMode.Osu, x => x.ApproachRate >= 8.8 && x.RiceCount > x.SliderCount)
        };

        var mapList = toDownload
            .SelectMany(x => x).ToList()
            .RemoveDownloadedBeatmaps();

        Console.WriteLine($"Downloading {mapList.Count} beatmaps");
        _cnt = mapList.Count;

        // create thread pool
        var queue = new ConcurrentQueue<(BeatmapInfoBase Map, int Count)>(mapList.Select(x => (x, 0)));
        var tasks = new Task[Environment.ProcessorCount];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => DownloadTask(queue, 10));
        }

        // wait for all tasks to finish
        await Task.WhenAll(tasks);
        Console.WriteLine("Download finished");
        Console.WriteLine($"Failed to download {_failed} beatmaps");
        Console.WriteLine($"Downloaded {mapList.Count - _failed} beatmaps");

        // open output folder in explorer
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start("explorer.exe", Output);
        }
    }
}