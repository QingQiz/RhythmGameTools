using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace OsuBeatmapExtractor;

public partial record BasicInfo(string Artist, string Title, string Version)
{
    public override string ToString()
    {
        return $"{Artist} - {Title} [{Version}]";
    }

    private static readonly string[] PackArtistFeature =
    {
        "V.A.", "Various Artists", "Various Artist", "VA",
    };

    private static readonly string[] PackArtistFeatureFull =
    {
        "VA",
    };

    private static readonly string[] PackTitleFeature =
    {
        "Pack", "Collect", "Course", "_IceRain", "Practice", "Project", "Convert", "train"
    };

    private static readonly string[] PackTitleFeatureNot =
    {
        "Dan Course"
    };

    private bool PackArtist()
    {
        return PackArtistFeature.Any(x => Artist.Contains(x, StringComparison.OrdinalIgnoreCase))
               || PackArtistFeatureFull.Any(x => Artist.Equals(x, StringComparison.OrdinalIgnoreCase));
    }

    private bool PackTitle()
    {
        return PackTitleFeature.Any(x => Title.Contains(x, StringComparison.OrdinalIgnoreCase))
               && PackTitleFeatureNot.All(x => !Title.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public BasicInfo Alter()
    {
        var artist  = Artist;
        var title   = Title;
        var version = Version;

        var matches = VersionExtMatcher().Matches(Version);

        // find [...] version
        var ext = matches
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        // remove [...] version
        version = ext.Aggregate(version, (current, e) => current.Replace(e, "")).Trim();

        // [mapper's diff] -> [diff]
        ext = ext.Select(e =>
        {
            e = e.Replace('\u0027', '\'');

            var match = VersionMapperEliminator().Match(e);
            return match.Success ? $"[{match.Groups[2].Value}]" : e;
        }).ToList();

        var mask = ext.Select(x => DiffMatcher().Match(x).Success).ToList();

        var diff = ext.Zip(mask, (e, m) => m ? e : "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        ext = ext.Zip(mask, (e, m) => m ? "" : e).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (PackArtist())
        {
            var match = ArtistMatcher().Match(version);

            if (match.Success)
            {
                artist = match.Groups[1].Value.Trim();
            }
        }

        if (PackTitle())
        {
            var toMatch = version;

            if (!string.IsNullOrWhiteSpace(artist) && toMatch.Contains(artist))
            {
                toMatch = toMatch.Replace(artist, "").Trim().Trim('-').Trim();
            }

            title = toMatch;
        }

        if (!string.IsNullOrWhiteSpace(artist)) version = version.Replace(artist, "");
        if (!string.IsNullOrWhiteSpace(title)) version  = version.Replace(title, "");

        version =  version.Trim().Trim('-').Trim();
        version += string.Join("", ext);

        return new BasicInfo(artist, string.Join("", diff) + title, version);
    }

    [GeneratedRegex("(.*?)\\s+-")]
    private static partial Regex ArtistMatcher();

    [GeneratedRegex(@"\[(?>(?<c>\[)|(?<-c>\])|[^\[\]]+)+?(?(c)(?!))\]")]
    private static partial Regex VersionExtMatcher();

    [GeneratedRegex(@"\[(.*)\s(\d+)\]")]
    private static partial Regex VersionMapperEliminator();

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex DiffMatcher();
};

public class ManiaBeatmapModifier
{
    private readonly string _beatmapPath;

    public readonly List<string> Lines = new();
    public readonly Dictionary<string, string> General = new();
    private readonly Dictionary<string, string> _metadata = new();
    private readonly Dictionary<string, string> _difficulty = new();


    private void ReadBeatmap()
    {
        var stat = -1;

        var lines = File.ReadAllLines(_beatmapPath);

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

    public bool UpdateMeta()
    {
        var basic = new BasicInfo(_metadata["Artist"], _metadata["Title"], _metadata["Version"]);
        
        var altered = basic.Alter();
        
        _metadata["Artist"]  = altered.Artist;
        _metadata["Title"]   = altered.Title;
        _metadata["Version"] = altered.Version;
        _metadata["ArtistUnicode"] = altered.Artist;
        _metadata["TitleUnicode"]  = altered.Title;
        return true;
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
            return fn.Trim();
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
}

public static class Program
{
    public static void Main(string[] args)
    {
        const string input  = @"O:\GameStorage\osu!\Songs";
        const string output = @"O:\extract";

        var dirs = Directory.GetDirectories(input);
        // dirs = new[] { "O:\\GameStorage\\osu!\\Songs\\1644396 Yuuki Noa (CV Kanako) - The Order" };

        _ = dirs.AsParallel().Select(dir =>
        {
            try
            {
                if (Path.GetFileName(dir).StartsWith("[")) return false;

                var files = Directory.GetFiles(dir, "*.osu");
                if (files.Length == 0) return false;

                var maps = files
                    .AsParallel()
                    .Select(f => new ManiaBeatmapModifier(f))
                    // skip easy maps
                    .Where(x => x.Lines.Count > 200)
                    // skip non-6k maps
                    .Where(x => x.Cs == 6)
                    .ToList();

                foreach (var map in maps)
                {
                    map.UpdateMeta();

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
        _ = Directory.GetDirectories(@"O:\extract").AsParallel().Select(dir =>
        {
            // zip dir to .osz
            var zip = dir + ".osz";
            if (File.Exists(zip)) return false;
            ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Fastest, false);
            return true;
        }).ToList();
    }


    // public static void Main(string[] args)
    // {
    // // deserialize Json
    // var list = JsonSerializer.Deserialize<List<BasicInfo>>(File.ReadAllText(@"O:\extract\list.json"))!;
    //
    // var res = new List<BasicInfo>();
    //
    // foreach (var map in list)
    // {
    //     res.Add(map.Alter());
    // }
    //
    // File.WriteAllText(@"O:\extract\list2.json", JsonSerializer.Serialize(res, new JsonSerializerOptions
    // {
    //     Encoder       = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    //     WriteIndented = true
    // }));
    // }
}