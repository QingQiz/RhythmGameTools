using System.Text;

public static class Program
{
    public static void Merge(string tsv, string txt)
    {
        var lines = File.ReadAllLines(tsv);
        var dict = new Dictionary<string, List<string>>();

        foreach (var line in lines)
        {
            var split = line.Split('\t');
            var key = split[0];

            if (!dict.ContainsKey(key))
                dict.Add(key, new List<string>());

            dict[key].AddRange(split[1..]
                .Select(x => x.Trim('"').Replace("\"\"", "\""))
                .Where(x => !string.IsNullOrWhiteSpace(x))
            );
        }

        var txtLines = File.ReadAllLines(txt);

        foreach (var i in txtLines)
        {
            var split = i.Split('\t');
            var key = split[0];
            var value = split[1];

            if (!dict.ContainsKey(key))
                dict.Add(key, new List<string>());

            dict[key].Add(value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value);
        }

        var sb = new StringBuilder();

        foreach (var (key, value) in dict)
        {
            sb.Append(key);
            sb.Append('\t');
            sb.AppendJoin('\t', value);
            sb.Append('\n');
        }

        File.WriteAllText(tsv, sb.ToString());

        File.Copy(txt, txt + ".bak", true);
        File.WriteAllText(txt, "");
    }

    public static void Main(string[] args)
    {
        Merge(@"C:\Users\sofee\RiderProjects\QQBOT\Marisa.Frontend\public\assets\maimai\aliases.tsv",
            @"C:\Users\sofee\Desktop\temp\maimai\MaiMaiSongAliasTemp.txt");
        Merge(@"C:\Users\sofee\RiderProjects\QQBOT\Marisa.Plugin.Shared\Resource\Chunithm\aliases.tsv",
            @"C:\Users\sofee\Desktop\temp\chunithm\ChunithmSongAliasTemp.txt");
    }
}