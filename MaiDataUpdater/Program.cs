using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Exception = System.Exception;

namespace MaiDataUpdater;

class Program
{
    private static readonly IConfigurationRoot Config =
        new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly()).Build();

    private static readonly string MaiSalt = Config["Mai:Salt"]!;
    private const string MaiHost = "maimai-gm.wahlap.com:42081";
    // public const string MaiHost = "localhost:12347";

    private const string AimeHost = "ai.sys-all.cn";
    private static readonly string AimeSalt = Config["Aime:Salt"]!;

    private const string WeChatId = "SGWC";
    private const string GameId = "MAID";

    private static readonly string AesKey = Config["Aes:Key"]!;
    private static readonly string AesIv = Config["Aes:IV"]!;

    public const int UserId = 0;

    // ReSharper disable InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Local
    private record UserIdRep(int errorID, string key, string timestamp, int userID);
    // ReSharper restore InconsistentNaming

    static async Task<int> GetUserId(string qrCodeResult)
    {
        if (qrCodeResult[..4] != WeChatId || qrCodeResult[4..8] != GameId)
        {
            throw new InvalidDataException("无效的");
        }

        var timestamp = qrCodeResult[8..20];
        var qrCode    = qrCodeResult[20..];

        var chipId = $"A63E-01E{Random.Shared.Next(999999999).ToString().PadLeft(8, '0')}";

        var key = BitConverter.ToString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{chipId}{timestamp}{AimeSalt}"))
        ).Replace("-", "").ToUpper();

        var data = JsonConvert.SerializeObject(new
        {
            chipID     = chipId,
            openGameID = GameId,
            key,
            qrCode,
            timestamp
        });

        var rep = await $"http://{AimeHost}/wc_aime/api/get_data"
            .WithHeaders(new
            {
                Host           = AimeHost,
                User_Agent     = "WC_AIME_LIB",
                Content_Length = data.Length,
            })
            .PostStringAsync(data);

        var result = await rep.GetJsonAsync<UserIdRep>();

        if (result.errorID != 0)
        {
            throw new InvalidDataException("获取ID失败，错误码：" + result.errorID);
        }

        return result.userID;
    }

    public static byte[] AesEncrypt(string data)
    {
        var key = Encoding.UTF8.GetBytes(AesKey);
        var iv  = Encoding.UTF8.GetBytes(AesIv);

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.IV      = iv;

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        var inputBytes     = Encoding.UTF8.GetBytes(data);
        var encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

        return encryptedBytes;
    }

    public static byte[] AesDecrypt(byte[] data)
    {
        var key = Encoding.UTF8.GetBytes(AesKey);
        var iv  = Encoding.UTF8.GetBytes(AesIv);

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = key;
        aes.IV      = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

        var decryptedBytes = decryptor.TransformFinalBlock(data, 0, data.Length);

        return decryptedBytes;
    }

    public static string Obfuscate(string data)
    {
        return BitConverter.ToString(
            MD5.HashData(Encoding.UTF8.GetBytes(data + MaiSalt))
        ).Replace("-", "").ToLower();
    }

    public static async Task<byte[]> Compress(byte[] data)
    {
        byte[] compressedData;

        // 将字符串转换为字节数组
        var originalBytes = data;

        using (var compressedStream = new MemoryStream())
        {
            await using (var zLibStream = new ZLibStream(compressedStream, CompressionMode.Compress))
            {
                zLibStream.Write(originalBytes, 0, originalBytes.Length);
            }

            compressedData = compressedStream.ToArray();
        }

        Console.WriteLine(compressedData.Length);

        return compressedData;
        // return BitConverter.ToString(compressedData).Replace("-", "").ToLower();
    }

    public static async Task<byte[]> Decompress(byte[] data)
    {
        using var       compressedStream   = new MemoryStream(data);
        await using var zLibStream         = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var       decompressedStream = new MemoryStream();

        await zLibStream.CopyToAsync(decompressedStream);

        var decompressedData = decompressedStream.ToArray();

        return decompressedData;
    }

    static async Task<byte[]> MakeMaiRequest(string api, string data, int userId)
    {
        var entry = Obfuscate(api);

        var body = await Compress(AesEncrypt(data));

        var httpClient = new HttpClient(new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.None
        });

        var cli = new FlurlClient(httpClient);

        cli.HttpClient.DefaultRequestHeaders.Clear();

        cli.WithHeaders(new
        {
            Host             = MaiHost,
            User_Agent       = $"{entry}#{userId}",
            charset          = "UTF-8",
            Mai_Encoding     = "1.30",
            Content_Encoding = "deflate",
            Content_Length   = body.Length,
        });

        var rep = await cli
            .Request($"https://{MaiHost}/Maimai2Servlet/{entry}")
            .PostAsync(new ByteArrayContent(body));

        var repStream = await rep.GetStreamAsync();
        var ms        = new MemoryStream();

        await repStream.CopyToAsync(ms);

        var bytes = ms.ToArray();

        return bytes;
    }

    static async Task<bool> Logout(int userId)
    {
        var req = JsonConvert.SerializeObject(new { userId });

        var rep = await MakeMaiRequest("UserLogoutApiMaimaiChn", req, userId);

        try
        {
            var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(res)!["returnCode"] == 1;
        }
        catch
        {
            return false;
        }
    }

    static async Task GetUserData(int userId)
    {
        var req = JsonConvert.SerializeObject(new
        {
            userId,
            maxCount = "2147483647",
            nextIndex = "0"
        });

        var rep = await MakeMaiRequest("GetUserMusicApiMaimaiChn", req, userId);
        Console.WriteLine(rep.Length);

        var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));
        Console.WriteLine(res);
    }

    static async Task GetUserPreview(int userId)
    {
        var req = JsonConvert.SerializeObject(new
        {
            userId,
        });

        var rep = await MakeMaiRequest("GetUserPreviewApiMaimaiChn", req, userId);
        Console.WriteLine(rep.Length);

        var res = Encoding.UTF8.GetString(AesDecrypt(await Decompress(rep)));
        Console.WriteLine(res);
    }


    static async Task Main(string[] args)
    {
        try
        {
            await GetUserData(UserId);
            // await GetUserPreview(UserId);
            // await Logout(UserId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        finally
        {
            Environment.Exit(0);
        }

        // GetUserId("SGWCMAID2405112010430058D95C0899E19AB611E0B33938165BD660FFDA8D68BD047D480AA9E6F3F52B").Wait();
        // Console.WriteLine(Obfuscate("asd"));
    }
}