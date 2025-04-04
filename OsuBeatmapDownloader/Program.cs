﻿using System.Collections.Concurrent;
using System.Globalization;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using Realms;

namespace OsuBeatmapDownloader;

internal record BeatmapSet(int Id, string Title, string Artist, string Creator);

internal static class Program
{
    private const string LazerPath = @"O:\GameStorage\osu!lazer";
    private const string Output = LazerPath;

    private static HashSet<int> _downloaded = [];

    private static async Task<List<BeatmapSet>> GetRankedBeatmapSets()
    {
        var list = new List<BeatmapSet>();
        var cache =
            from file in Directory.GetFiles(".", "*-ranked.json")
            select JsonConvert.DeserializeObject<List<BeatmapSet>>(File.ReadAllTextAsync(file).Result);
        foreach (var i in cache) list.AddRange(i);
        list = list.DistinctBy(x => x.Id).ToList();

        while (true)
        {
            var date = (
                from file in Directory.GetFiles(".", "*-ranked.json")
                select Path.GetFileNameWithoutExtension(file)
                into dateInFile
                select dateInFile.Split('-')[0]
                into dateInFile
                select DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(dateInFile))
                into last
                select last - TimeSpan.FromHours(2)
            ).Aggregate(DateTimeOffset.MinValue, (current, last) => last > current ? last : current);

            const int limit = 500;
            var url = "https://osu.ppy.sh/api/get_beatmaps".SetQueryParams(new
            {
                k = OsuApi.Config["apiKey"],
                // utc time in mysql format
                since = date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                m     = 3,
                s     = 0,
                limit
            });

            Console.WriteLine("Requesting maps ranked after " + date.ToString("yyyy-MM-dd HH:mm:ss"));

            var resString = await url.GetStringAsync();
            var json      = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(resString);

            var listSub = json.Select(b =>
                new BeatmapSet(int.Parse(b["beatmapset_id"]), b["title"], b["artist"], b["creator"])
            ).ToList();

            list.AddRange(listSub);
            list = list.DistinctBy(x => x.Id).ToList();

            var maxT = json.Max(x =>
                DateTime.ParseExact(x["approved_date"], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            );

            var cacheFileName = $"{new DateTimeOffset(maxT).ToUnixTimeMilliseconds()}-ranked.json";
            await File.WriteAllTextAsync(cacheFileName, JsonConvert.SerializeObject(listSub));

            if (json.Count < limit) return list;
        }
    }

    private static bool HandleException(Exception e)
    {
        while (e is AggregateException ae)
        {
            e = ae.InnerExceptions.First();
        }
        if (e is not FlurlHttpException fe) return false;

        Console.WriteLine(fe.Message);
        return true;
    }

    private static async Task<List<BeatmapSet>> GetUserBeatmapSets(string username)
    {
        var cacheName = DateTime.Now.ToString("[yyyy-MM-dd]") + $"[{username}].json";
        if (File.Exists(cacheName))
        {
            return JsonConvert.DeserializeObject<List<BeatmapSet>>(await File.ReadAllTextAsync(cacheName));
        }

        var user = await OsuApi.Request(OsuApi.ApiRoot + $"/users/{username}/mania?key=username").GetJsonAsync();
        var uri  = OsuApi.ApiRoot + $"/users/{user.id}/beatmapsets";

        var type = new[] { "graveyard", "guest", "loved", "ranked" };

        var res = new List<BeatmapSet>();

        foreach (var t in type)
        {
            const int limit  = 50;
            var       offset = 0;

            while (true)
            {
                try
                {
                    var j = await OsuApi
                        .Request($"{uri}/{t}?limit={limit}&offset={offset}")
                        .GetJsonListAsync();

                    res.AddRange(j.Select(x =>
                        new BeatmapSet((int)x.id, (string)x.title, (string)x.artist, (string)x.creator))
                    );
                    if (j.Count < limit) break;
                }
                catch (Exception e)
                {
                    if (HandleException(e)) continue;
                    break;
                }

                offset += limit;
            }
        }

        await File.WriteAllTextAsync(cacheName, JsonConvert.SerializeObject(res));

        return res;
    }

    private static async Task<List<BeatmapSet>> GetRecommendMapList()
    {
        var cacheName = DateTime.Now.ToString("[yyyy-MM-dd]") + "beatmaps.json";

        if (File.Exists(cacheName))
        {
            // deserialize
            return JsonConvert.DeserializeObject<List<BeatmapSet>>(await File.ReadAllTextAsync(cacheName));
        }

        var userList = new[]
        {
            // 7k
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
            // 6k
            "QQwiwi2012", "Arkman", "[Crz]Derrick", "doubu","_IceRain", "[Crz]Emperor", "Alipay", "HMillion", "Benson_",
            "[Crz]sunnyxxy"
        };

        var mapList = new List<BeatmapSet>();

        Parallel.ForEach(userList.Distinct(), new ParallelOptions { MaxDegreeOfParallelism = 8 }, user =>
        {
            RETRY:
            try
            {
                Console.WriteLine("Requesting " + user);
                var res = GetUserBeatmapSets(user).Result;
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
        var fp    = Path.Join(LazerPath, "client.realm");
        var fpNew = Path.Join(LazerPath, "client.realm.new");
        File.Copy(fp, fpNew, true);

        using (var realm = Realm.GetInstance(new RealmConfiguration(fpNew) { SchemaVersion = 4700 }))
        {
            var info = realm.All<BeatmapSetInfo>().ToList().Select(x => x.OnlineID);
            _downloaded = info.ToHashSet();
        }
        File.Delete(fpNew);
        File.Delete(fpNew + ".lock");

        return mapList
            .DistinctBy(x => x.Id)
            .Where(x => !_downloaded.Contains(x.Id))
            .ToList();
    }

    private static int _cnt;
    private static readonly object CntLk = new();
    private static void DownloadTask(ConcurrentQueue<(BeatmapSet Map, int Count)> queue, int maxRetry)
    {
        while (queue.TryDequeue(out var map))
        {
            try
            {
                OsuApi.DownloadBeatmap(map.Map.Id, Output).GetAwaiter().GetResult();
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
        var rankedMapList    = await GetRankedBeatmapSets();

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