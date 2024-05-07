using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Flurl.Http;

namespace AquaAimeIdFetcher;

static class Program
{
    private const string Key = "Copyright(C)SEGA";
    private const string KeyChipId = "SET YOUR KEYCHIP";
    private const string AccessCode = "Your Aime Card Access Code";
    private const string AquaHost = "ea.naominet.live";

    static string ToHex(this byte[] bytes)
    {
        return bytes.Select(b => b.ToString("X2")).Aggregate((x, y) => x + y);
    }

    static byte[] AsciiToBytes(this string str)
    {
        var bytes = new byte[str.Length];

        for (var i = 0; i < str.Length; i++)
        {
            bytes[i] = (byte)str[i];
        }

        return bytes;
    }

    static byte[] HexToBytes(this string hex)
    {
        var bytes = new byte[hex.Length / 2];

        for (var i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber);
        }

        return bytes;
    }

    static async Task Main()
    {
        Console.WriteLine(await GetServerUri(KeyChipId));
        Console.WriteLine(AccessCodeToAimeId(AccessCode, KeyChipId));
    }

    private static async Task<string> GetServerUri(string keyChipId)
    {
        var data = $"game_id=SDHD&ver=2.20&serial={keyChipId}";

        byte[] compressedData;

        // 将字符串转换为字节数组
        var originalBytes = Encoding.UTF8.GetBytes(data);

        using (var compressedStream = new MemoryStream())
        {
            await using (var zLibStream = new ZLibStream(compressedStream, CompressionMode.Compress))
            {
                zLibStream.Write(originalBytes, 0, originalBytes.Length);
            }

            compressedData = compressedStream.ToArray();
        }

        var postData = Convert.ToBase64String(compressedData);

        var resp     = await $"http://{AquaHost}/sys/servlet/PowerOn".PostStringAsync(postData);
        var respData = await resp.GetStringAsync();

        return respData.Split('&').First(x => x.StartsWith("uri")).Split('=', 2)[1];
    }

    private static int AccessCodeToAimeId(string accessCode, string keyChipId)
    {
        var bytes    = GenerateRequestBytes(accessCode, keyChipId);
        var outBytes = new byte[48];

        var ip = Dns.GetHostAddresses(AquaHost)[0];
        using (var client = new TcpClient())
        {
            client.Connect(ip, 22345);
            using (var stream = client.GetStream())
            {
                client.Client.NoDelay = true;
                stream.Write(bytes, 0, bytes.Length);

                _ = client.GetStream().Read(outBytes, 0, outBytes.Length);

                client.GetStream().Close();
                client.Close();
            }
        }

        return DecryptResponse(outBytes);
    }

    static byte[] GenerateRequestBytes(string accessCode, string keyChipId)
    {
        if (accessCode.Length != 20) throw new InvalidDataException("Access code length must be 20");

        var aes = Aes.Create();

        aes.Key     = Key.AsciiToBytes();
        aes.IV      = new byte[16];
        aes.Mode    = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        // keyChipId to hex
        var kc = keyChipId.Replace("-", "").AsciiToBytes().ToHex().PadLeft(30, '0');

        var request = $"3ea1ab150f003000000153444844000005{kc}{accessCode}000000000000".HexToBytes();

        var encryptedBytes = new byte[48];
        aes.CreateEncryptor().TransformBlock(request, 0, request.Length, encryptedBytes, 0);

        return encryptedBytes;
    }

    static int DecryptResponse(byte[] bytes)
    {
        var aes = Aes.Create();

        aes.Key     = Key.AsciiToBytes();
        aes.IV      = new byte[16];
        aes.Mode    = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        var decryptedBytes = new byte[bytes.Length];

        aes.CreateDecryptor().TransformBlock(bytes, 0, bytes.Length, decryptedBytes, 0);

        var idHex = decryptedBytes[32..45].Reverse().ToArray().ToHex();

        return int.TryParse(idHex, NumberStyles.HexNumber, null, out var id) ? id : -1;
    }
}