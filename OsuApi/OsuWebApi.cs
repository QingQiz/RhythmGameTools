using System.Globalization;
using System.Net;
using System.Reflection;
using System.Web;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace OsuApi;

public static class OsuWebApi
{
    #region req with auth

    private static string? _token;
    private static DateTime? _tokenExpire;
    private static readonly Lock TokenLock = new();

    private static string Token
    {
        get
        {
            lock (TokenLock)
            {
                if (_tokenExpire == null || DateTime.Now > _tokenExpire || _token == null) RenewToken().Wait();
            }

            return _token!;
        }
    }

    private const string TokenUri = "https://osu.ppy.sh/oauth/token";

    private const string FakeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";

    private const string ApiRoot = "https://osu.ppy.sh/api/v2";

    private static readonly IConfigurationRoot Config =
        new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly()).Build();

    /// <summary>
    /// 更新 token
    /// </summary>
    private static async Task RenewToken()
    {
        var clientId     = Config["clientId"];
        var clientSecret = Config["clientSecret"];

        var response = await TokenUri.PostJsonAsync(new
        {
            grant_type    = "client_credentials",
            client_id     = clientId,
            client_secret = clientSecret,
            scope         = "public"
        });

        var res = await response.GetJsonAsync();

        _token       = res.access_token;
        _tokenExpire = DateTime.Now + TimeSpan.FromSeconds(res.expires_in);
    }

    private static IFlurlRequest Request(string uri)
    {
        return uri.WithHeader("Accept", "application/json")
            .WithOAuthBearerToken(Token);
    }

    #endregion

    #region Beatmap Downloader

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression                    = DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    })
    {
        Timeout = TimeSpan.FromMinutes(2),
    };

    // 从 sayobot 镜像下载 beatmap
    public static async Task<string> DownloadBeatmap(long beatmapSetId, string path)
    {
        return await DownloadBeatmapInner();

        async Task<string> DownloadBeatmapInner()
        {
            using var request = new HttpRequestMessage(new HttpMethod("GET"),
                $"https://dl.sayobot.cn/beatmaps/download/full/{beatmapSetId}");
            request.Headers.TryAddWithoutValidation("authority", "dl.sayobot.cn");
            request.Headers.TryAddWithoutValidation("accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            request.Headers.TryAddWithoutValidation("accept-language", "zh-CN,zh;q=0.9,en-GB;q=0.8,en;q=0.7");
            request.Headers.TryAddWithoutValidation("sec-ch-ua",
                "\"Google Chrome\";v=\"105\", \"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"105\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "none");
            request.Headers.TryAddWithoutValidation("sec-fetch-user", "?1");
            request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            request.Headers.TryAddWithoutValidation("user-agent", FakeUserAgent);

            var response = await HttpClient.SendAsync(request);

            var filename = (HttpUtility.ParseQueryString(request.RequestUri!.Query).Get("filename") ??
                            beatmapSetId.ToString()) + ".osz";

            // check if is valid filename
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                filename = beatmapSetId + ".osz";
            }

            var beatmapPath = Path.Join(path, filename);

            var s  = await response.Content.ReadAsStreamAsync();
            var fs = File.OpenWrite(beatmapPath);

            await s.CopyToAsync(fs);

            s.Close();
            fs.Close();

            // check file size. at least 10KB
            if (new FileInfo(beatmapPath).Length < 10240)
            {
                File.Delete(beatmapPath);
                throw new Exception("Download failed");
            }

            return beatmapPath;
        }
    }

    #endregion

    #region high level api

    public static async Task<List<BeatmapInfoV1>> GetRankedBeatmapSets(GameMode mode, Func<BeatmapInfoV1, bool>? filter = null)
    {
        var fnSuffix = $"-ranked-{Enum.GetName(mode)}.json";

        var list = Directory.GetFiles(".", $"*{fnSuffix}")
            .AsParallel()
            .Select(file => JsonConvert.DeserializeObject<List<BeatmapInfoV1>>(File.ReadAllTextAsync(file).Result))
            .SelectMany(x => x!)
            .DistinctBy(x => x.BeatmapSetId).ToList();

        while (true)
        {
            var date = (
                from file in Directory.GetFiles(".", $"*{fnSuffix}")
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
                k = Config["apiKey"],
                // utc time in mysql format
                since = date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                m     = mode,
                s     = 0,
                limit
            });

            Console.WriteLine($"Requesting maps {Enum.GetName(mode)} ranked maps after " + date.ToString("yyyy-MM-dd HH:mm:ss"));

            var resString = await url.GetStringAsync();
            var json      = JsonConvert.DeserializeObject<List<BeatmapInfoV1>>(resString)!;

            list.AddRange(json);
            list = list.DistinctBy(x => x.BeatmapSetId).ToList();

            var maxT = json.Max(x =>
                DateTime.ParseExact(x.ApprovedDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            );

            var cacheFileName = $"{new DateTimeOffset(maxT).ToUnixTimeMilliseconds()}{fnSuffix}";
            await File.WriteAllTextAsync(cacheFileName, JsonConvert.SerializeObject(json));

            if (json.Count < limit) return filter == null ? list : list.Where(filter).ToList();
        }
    }

    public static async Task<List<BeatmapInfoV2>> GetUserBeatmapSets(string username)
    {
        var now = DateTime.Now;
        var fn  = $"[Y{now.Year}M{now.Month}W{now.Day / 7}][{username}].json";
        if (File.Exists(fn))
        {
            return JsonConvert.DeserializeObject<List<BeatmapInfoV2>>(await File.ReadAllTextAsync(fn))!;
        }

        var user = await Request(ApiRoot + $"/users/{username}/mania?key=username").GetJsonAsync();
        var uri  = ApiRoot + $"/users/{user.id}/beatmapsets";

        var type = new[] { "graveyard", "guest", "loved", "ranked" };

        var res = new List<BeatmapInfoV2>();

        foreach (var t in type)
        {
            const int limit  = 50;
            var       offset = 0;

            while (true)
            {
                try
                {
                    var jText = await Request($"{uri}/{t}?limit={limit}&offset={offset}").GetStringAsync();
                    var json  = JsonConvert.DeserializeObject<List<BeatmapInfoV2>>(jText)!;
                    res.AddRange(json);
                    if (json.Count < limit) break;
                }
                catch (Exception e)
                {
                    if (HandleException(e)) continue;
                    break;
                }

                offset += limit;
            }
        }

        await File.WriteAllTextAsync(fn, JsonConvert.SerializeObject(res));

        return res;

        bool HandleException(Exception e)
        {
            while (e is AggregateException ae)
            {
                e = ae.InnerExceptions.First();
            }
            if (e is not FlurlHttpException fe) return false;

            Console.WriteLine(fe.Message);
            return true;
        }
    }

    #endregion
}