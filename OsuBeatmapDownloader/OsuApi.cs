using System.Net;
using System.Reflection;
using System.Web;
using Flurl.Http;
using Microsoft.Extensions.Configuration;

namespace OsuBeatmapDownloader;

public static class OsuApi
{
    private static string? _token;
    private static DateTime? _tokenExpire;

    private static string Token
    {
        get
        {
            if (_tokenExpire == null || DateTime.Now > _tokenExpire || _token == null)
            {
                RenewToken().Wait();
            }

            return _token!;
        }
    }

    private const string TokenUri = "https://osu.ppy.sh/oauth/token";

    private const string FakeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";

    public const string ApiRoot = "https://osu.ppy.sh/api/v2";

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

    public static IFlurlRequest Request(string uri)
    {
        return uri.WithHeader("Accept", "application/json")
            .WithOAuthBearerToken(Token);
    }

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

        return await DownloadBeatmapInner();
    }

    #endregion
}