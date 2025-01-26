using System.Text.RegularExpressions;

namespace Extract6kBeatmap;

public static partial class Program
{
    private static readonly Regex Matcher = MyRegex();

    private static bool TryGetBeatmapCover(string beatmapPath, out string? coverPath)
    {
        var lines = File.ReadLines(beatmapPath);

        foreach (var line in lines)
        {
            if (Matcher.Match(line) is not { Success: true } match) continue;

            coverPath = Path.Join(Path.GetDirectoryName(beatmapPath), match.Groups[1].Value);
            return true;
        }

        coverPath = null;
        return false;
    }
    
    private static bool TryGetAudioFileName(string beatmapPath, out string? audioFileName)
    {
        var lines = File.ReadLines(beatmapPath);

        foreach (var line in lines)
        {
            if (!line.StartsWith("AudioFilename:")) continue;

            audioFileName = line.Split(':')[1].Trim();
            return true;
        }

        audioFileName = null;
        return false;
    }

    public static void Main()
    {
        const string prefix = @"F:\workspace\KeyTransfer\data\beatmaps";

        Parallel.ForEach(Directory.GetDirectories(@"O:\GameStorage\osu!\Songs"), d =>
        {
            var split = Path.GetFileName(d).Split(' ', 2);
            if (split.Length != 2) return;

            if (!int.TryParse(split[0], out var id)) return;

            var parent = Path.Join(prefix, id.ToString());
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            foreach (var f in Directory.GetFiles(d, "*.osu", SearchOption.TopDirectoryOnly))
            {
                var beatmapPath = Path.Join(parent, Path.GetFileName(f));

                if (!File.Exists(beatmapPath)) File.Copy(f, beatmapPath);

                // if (!TryGetBeatmapCover(f, out var coverPath)) continue;
                //
                // var toCp = Path.Join(parent, Path.GetFileName(coverPath));
                //
                // if (!File.Exists(coverPath)) continue;
                // if (File.Exists(toCp)) continue;
                //
                // Directory.CreateDirectory(Path.GetDirectoryName(toCp)!);
                // File.Copy(coverPath!, toCp);
            }
        });
    }

    [GeneratedRegex("^\\s*\\d+\\s*,\\s*\\d+\\s*,\\s*\"(.*)\"\\s*,\\s*\\d+\\s*,\\s*\\d+\\s*$")]
    private static partial Regex MyRegex();
}
