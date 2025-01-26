using System.Dynamic;
using System.Text;
using System.Text.Json;
using Flurl.Http;

namespace BotSongAliasUpdater;

public static class Program
{
    private static HashSet<string> GetDefault(string key)
    {
        var simplified = ConvertToSimplified(key);

        return simplified.Equals(key, StringComparison.OrdinalIgnoreCase)
            ? new HashSet<string>()
            : new HashSet<string> { ConvertToSimplified(key) };
    }

    private static string Escape(string s)
    {
        return s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }

    private static string Trim(string s)
    {
        if (s[0] == '"' && s[^1] == '"') return s[1..^1];
        return s;
    }

    private static void Merge(HashSet<string> titles, string tsv, string txt)
    {
        var lines = File.ReadAllLines(tsv);
        var dict  = new Dictionary<string, HashSet<string>>();

        foreach (var line in lines)
        {
            var split = line.Split('\t');
            var key   = split[0];

            foreach (var i in split[1..]
                         .Select(x => Trim(x).Replace("\"\"", "\""))
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!dict.ContainsKey(key)) dict[key] = GetDefault(key);
                dict[key].Add(i);
            }
        }

        var txtLines = File.ReadAllLines(txt);

        foreach (var i in txtLines)
        {
            var split = i.Split('\t');
            var key   = Escape(split[0]);
            var value = split[1];

            if (!dict.ContainsKey(key)) dict[key] = GetDefault(key);
            dict[key].Add(value);
        }

        foreach (var t in titles.Select(Escape).Where(t => !dict.ContainsKey(t)))
        {
            dict[t] = GetDefault(t);
        }

        foreach (var key in dict.Keys)
        {
            var set      = dict[key].ToList();
            var toRemove = new HashSet<string>();

            for (var i = 0; i < set.Count; i++)
            {
                var toCheck = set[i];

                if (toCheck.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    toRemove.Add(toCheck);
                    continue;
                }

                for (var j = 0; j < i; j++)
                {
                    var cmp = set[j];

                    if (toRemove.Contains(cmp)) continue;
                    if (!cmp.Contains(toCheck, StringComparison.OrdinalIgnoreCase)) continue;

                    toRemove.Add(toCheck);
                    break;
                }
            }

            foreach (var i in toRemove)
            {
                dict[key].Remove(i);
            }
        }

        var sb = new StringBuilder();

        foreach (var (key, value) in dict)
        {
            if (!value.Any()) continue;

            sb.Append(key);
            sb.Append('\t');
            sb.AppendJoin('\t', value.Select(v => v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v));
            sb.Append('\n');
        }

        File.WriteAllText(tsv, sb.ToString());
    }

    private static Dictionary<char, char>? _mapJ2T;

    private static Dictionary<char, char> MapJ2T => _mapJ2T ??= File.ReadAllLines("dict.txt")
        .Select(x => x.Split('\t'))
        .GroupBy(x => x[1])
        .Where(x => x.Count() == 1)
        .ToDictionary(x => x.Key[0], x => x.First()[0][0]);

    private static Dictionary<char, char>? _mapT2S;

    private static Dictionary<char, char> MapT2S => _mapT2S ??= File.ReadAllLines("dict2.txt")
        .Select(x => x.Split('\t'))
        .GroupBy(x => x[0][0])
        .Where(x => x.Count() == 1)
        .ToDictionary(x => x.Key, x => x.First()[1][0]);

    static string ConvertToSimplified(string traditionalChinese)
    {
        var sbT = new StringBuilder();
        var sbS = new StringBuilder();

        foreach (var c in traditionalChinese) sbT.Append(MapJ2T.ContainsKey(c) ? MapJ2T[c] : c);
        foreach (var c in sbT.ToString()) sbS.Append(MapT2S.ContainsKey(c) ? MapT2S[c] : c);

        return sbS.ToString();
    }

    private static string WinPath2Wsl(string path)
    {
        var x = path.Split(':', 2);
        x[0] = x[0].ToLower();
        x[1] = x[1].Replace('\\', '/');
        return "/mnt/" + x[0] + x[1];
    }
    
    private static string WslPath2Win(string path)
    {
        var x = path.Split('/', 4);
        x[2] = x[2].ToUpper();
        x[3] = x[3].Replace('/', '\\');
        return x[2] + ":" + x[3];
    }

    public static async Task Main()
    {
        const string maiFile = "MaiMaiSongAliasTemp.txt";
        const string chuFile = "ChunithmSongAliasTemp.txt";

        var tempPath = Path.GetTempPath();
        var tempPathWsl = WinPath2Wsl(tempPath);
        var p1       = System.Diagnostics.Process.Start("wsl", $"scp tx:/home/ubuntu/bot/MarisaBotTemp/maimai/{maiFile} {tempPathWsl}");
        var p2       = System.Diagnostics.Process.Start("wsl", $"scp tx:/home/ubuntu/bot/MarisaBotTemp/chunithm/{chuFile} {tempPathWsl}");
        p1.Start();
        p2.Start();
        await p1.WaitForExitAsync();
        await p2.WaitForExitAsync();
        
        // empty file in server
        p1 = System.Diagnostics.Process.Start("wsl", $"ssh tx 'echo > /home/ubuntu/bot/MarisaBotTemp/maimai/{maiFile}'");
        p2 = System.Diagnostics.Process.Start("wsl", $"ssh tx 'echo > /home/ubuntu/bot/MarisaBotTemp/chunithm/{chuFile}'");
        p1.Start();
        p2.Start();
        await p1.WaitForExitAsync();
        await p2.WaitForExitAsync();

        var chuMd = (JsonSerializer.Deserialize<ExpandoObject[]>(
                await File.ReadAllTextAsync("C:/Users/sofee/Desktop/workspace/QQBOT/Marisa.Frontend/public/assets/chunithm/SongInfo.json")
            )! as dynamic[])
            .Select(x => x.Title.ToString()).Where(x => x != null).Cast<string>().ToHashSet();
        
        var maiMd = (await "https://www.diving-fish.com/api/maimaidxprober/music_data"
                .GetJsonAsync<ExpandoObject[]>() as dynamic[])
            .Select(x => x.title.ToString()).Where(x => x != null).Cast<string>().ToHashSet();
        
        Merge(maiMd, @"C:\Users\sofee\Desktop\workspace\QQBOT\Marisa.Frontend\public\assets\maimai\aliases.tsv",
            $@"{tempPath}\{maiFile}");
        
        Merge(chuMd, @"C:\Users\sofee\Desktop\workspace\QQBOT\Marisa.Frontend\public\assets\chunithm\aliases.tsv",
            $@"{tempPath}\{chuFile}");
    }
}