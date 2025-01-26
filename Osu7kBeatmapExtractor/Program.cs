using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Skinning;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace Osu7kBeatmapExtractor;

public class ManiaBeatmapModifier
{
    private readonly string _beatmapPath;

    public readonly List<string> Lines = [];
    public readonly Dictionary<string, string> General = new();
    private readonly Dictionary<string, string> _metadata = new();
    private readonly Dictionary<string, string> _difficulty = new();

    private double GetStarRating()
    {
        var ruleset = new ManiaRuleset();

        var workingBeatmap = ProcessorWorkingBeatmap.FromFile(_beatmapPath);
        var attributes     = ruleset.CreateDifficultyCalculator(workingBeatmap).Calculate([]);

        return attributes.StarRating;
    }

    public double PrependDiffToTitle()
    {
        var sr = GetStarRating();
        if (sr >= 100) return sr;

        _metadata["Title"]        = $"[{sr:00.00}] {_metadata["Title"]}";
        _metadata["TitleUnicode"] = $"[{sr:00.00}] {_metadata["TitleUnicode"]}";
        return sr;
    }

    private void ReadBeatmap()
    {
        var stat = -1;

        var data  = File.ReadAllText(_beatmapPath);
        var lines = data.Split('\n');

        Checksum = BitConverter.ToString(
            MD5.HashData(Encoding.UTF8.GetBytes(data))
        ).Replace("-", "");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("[General]", StringComparison.OrdinalIgnoreCase))
            {
                stat = 1;
                continue;
            }

            if (line.StartsWith("[Metadata]", StringComparison.OrdinalIgnoreCase))
            {
                stat = 2;
                continue;
            }

            if (line.StartsWith("[Difficulty]", StringComparison.OrdinalIgnoreCase))
            {
                stat = 3;
                continue;
            }

            if (line.StartsWith('['))
            {
                stat = 0;
            }

            switch (stat)
            {
                case 0:
                    Lines.Add(line);
                    continue;
                case -1:
                    continue;
                default:
                {
                    var kv = line.Split(":", 2).Select(x => x.Trim()).ToArray();
                    switch (stat)
                    {
                        case 1:
                            General[kv[0]] = kv[1];
                            break;
                        case 2:
                            _metadata[kv[0]] = kv[1];
                            break;
                        case 3:
                            _difficulty[kv[0]] = kv[1];
                            break;
                    }

                    break;
                }
            }
        }
    }

    public void Write(string dst)
    {
        var sb = new StringBuilder();

        sb.AppendLine("osu file format v14");

        sb.AppendLine("[General]");
        foreach (var (k, v) in General)
        {
            sb.AppendLine($"{k}: {v}");
        }

        sb.AppendLine("[Metadata]");
        foreach (var (k, v) in _metadata)
        {
            sb.AppendLine($"{k}: {v}");
        }

        sb.AppendLine("[Difficulty]");
        foreach (var (k, v) in _difficulty)
        {
            sb.AppendLine($"{k}: {v}");
        }

        foreach (var line in Lines)
        {
            sb.AppendLine(line);
        }

        File.WriteAllText(dst, sb.ToString());
    }

    public ManiaBeatmapModifier(string path)
    {
        _beatmapPath = path;
        ReadBeatmap();
    }

    public string Background
    {
        get
        {
            var bg = Lines
                .FirstOrDefault(x => x.StartsWith("0,0,\"", StringComparison.OrdinalIgnoreCase));

            return bg == null ? "" : bg.Split(',')[2].Trim('"');
        }
    }

    public string Fn
    {
        get
        {
            var fn = string.IsNullOrWhiteSpace(_metadata["Version"])
                ? $"{_metadata["Artist"]} - {_metadata["Title"]}"
                : $"{_metadata["Artist"]} - {_metadata["Title"]} [{_metadata["Version"]}]";

            fn = Path.GetInvalidFileNameChars().Aggregate(fn, (current, c) => current.Replace(c, '_'));
            fn = fn.Replace('/', '_');
            fn = fn.Replace('\\', '_');
            return $"[{Checksum}] {fn.Trim()}";
        }
    }

    public string Dn
    {
        get
        {
            var dn = $"{_metadata["BeatmapSetID"]} - {_metadata["Artist"]} - {_metadata["Title"]}";

            dn = Path.GetInvalidFileNameChars().Aggregate(dn, (current, c) => current.Replace(c, '_'));
            dn = dn.Replace('/', '_');
            dn = dn.Replace('\\', '_');
            return dn.TrimEnd('.').Trim().TrimEnd('.').Trim();
        }
    }

    public int Cs => (int)float.Parse(_difficulty["CircleSize"]);
    public int Mode => int.Parse(General["Mode"]);
    public string Checksum { get; private set; } = "";
}

public static class Program
{
    public static void Main()
    {
        const string input  = @"O:\GameStorage\osu!\Songs";
        const string output = @"O:\extract7k";

        var dirs = Directory.GetDirectories(input);
        // dirs = new[] { "O:\\GameStorage\\osu!\\Songs\\1644396 Yuuki Noa (CV Kanako) - The Order" };

        var exits = Directory.GetFiles(output, "*.osu", SearchOption.AllDirectories)
            .Select(x => Path.GetFileName(x).Split(' ', 2)[0][1..^1])
            .ToHashSet();

        _ = dirs.AsParallel().Select(dir =>
        {
            try
            {
                if (Path.GetFileName(dir).StartsWith('[')) return false;

                var files = Directory.GetFiles(dir, "*.osu");
                if (files.Length == 0) return false;

                var maps = files
                    .AsParallel()
                    .Select(f => new ManiaBeatmapModifier(f))
                    // skip easy maps
                    .Where(x => x.Lines.Count > 200)
                    // skip non-7k maps
                    .Where(x => x is { Mode: 3, Cs: 7 })
                    .ToList();

                foreach (var map in maps.Where(map => !exits.Contains(map.Checksum)))
                {
                    var sr = map.PrependDiffToTitle();
                    if (sr is < 4 or > 11) continue;

                    var audio = map.General["AudioFilename"];
                    var bg    = map.Background;
                    var fn    = map.Fn;
                    var dn    = map.Dn;

                    var path = Path.Join(output, dn);
                    Directory.CreateDirectory(path);

                    try
                    {
                        if (!File.Exists(Path.Join(path, audio)))
                        {
                            File.Copy(Path.Join(dir, audio), Path.Join(path, audio));
                        }

                        if (!string.IsNullOrWhiteSpace(bg) && !File.Exists(Path.Join(path, bg)))
                        {
                            File.Copy(Path.Join(dir, bg), Path.Join(path, bg));
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }

                    map.Write(Path.Join(path, $"{fn}.osu"));
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(dir);
                Console.WriteLine(e.ToString());
                Console.WriteLine("====================================");
                return false;
            }
        }).ToList();

        Console.WriteLine("zip..");
        _ = Directory.GetDirectories(output).AsParallel().Select(dir =>
        {
            // zip dir to .osz
            var zip = dir + ".osz";
            if (File.Exists(zip)) return false;
            ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Fastest, false);
            return true;
        }).ToList();
    }
}

internal static class LegacyHelper
{
    public static Ruleset GetRulesetFromLegacyId(int id)
    {
        return id switch
        {
            3 => new ManiaRuleset(),
            _ => throw new ArgumentException("Invalid ruleset ID provided.")
        };
    }

}

internal class ProcessorWorkingBeatmap : WorkingBeatmap
{
    private readonly Beatmap _beatmap;

    /// <summary>
    ///     Constructs a new <see cref="ProcessorWorkingBeatmap" /> from a .osu file.
    /// </summary>
    /// <param name="file">The .osu file.</param>
    /// <param name="beatmapId">An optional beatmap ID (for cases where .osu file doesn't have one).</param>
    private ProcessorWorkingBeatmap(string file, int? beatmapId = null)
        : this(ReadFromFile(file), beatmapId)
    {
    }

    private ProcessorWorkingBeatmap(Beatmap beatmap, int? beatmapId = null)
        : base(beatmap.BeatmapInfo, null)
    {
        _beatmap                    = beatmap;
        beatmap.BeatmapInfo.Ruleset = LegacyHelper.GetRulesetFromLegacyId(beatmap.BeatmapInfo.Ruleset.OnlineID).RulesetInfo;

        if (beatmapId.HasValue) beatmap.BeatmapInfo.OnlineID = beatmapId.Value;
    }

    private static Beatmap ReadFromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        using var reader = new LineBufferedReader(stream);
        return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
    }

    public static ProcessorWorkingBeatmap FromFile(string file)
    {
        if (!File.Exists(file)) throw new ArgumentException($"Beatmap file {file} does not exist.");

        return new ProcessorWorkingBeatmap(file);
    }

    protected override IBeatmap GetBeatmap()
    {
        return _beatmap;
    }

    public override Texture GetBackground()
    {
        throw new NotImplementedException();
    }

    protected override Track GetBeatmapTrack()
    {
        throw new NotImplementedException();
    }

    protected override ISkin GetSkin()
    {
        throw new NotImplementedException();
    }

    public override Stream GetStream(string storagePath)
    {
        throw new NotImplementedException();
    }
}