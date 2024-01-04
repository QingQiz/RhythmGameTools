using System.Xml;
using Newtonsoft.Json;
using File = System.IO.File;
using Formatting = Newtonsoft.Json.Formatting;

namespace OngekiSongListExtractor;

public static class Program
{
    private const string Root = @"F:\AppData\QQ\Tencent Files\642191352\FileRecv\ongeki music";
    private const string CoverRoot = @"F:\AppData\QQ\Tencent Files\642191352\nt_qq\nt_data\File\Ori\cover";
    private const string OutputPath = @"C:\Users\sofee\RiderProjects\QQBOT\Marisa.Frontend\public\assets\ongeki";

    private record MusicData(
        int Id,
        string Title,
        string Artist,
        string Source,
        string Genre,
        string BossCard,
        int BossLevel,
        string Version,
        string ReleaseDate,
        string CopyRight,
        List<Chart?> Charts
    );

    private record Chart(
        string Creator,
        decimal Const,
        string Bpm,
        // NoteCount = TapCount + HoldCount + SHoldCount + FlickCount
        int NoteCount,
        int TapCount,
        int HoldCount,
        int SideCount,
        int SHoldCount,
        int FlickCount,
        int BellCount
    );

    private static Chart ParseOngekiChart(string ogkr, decimal @const)
    {
        // read ogkr line by line
        var lines = File.ReadLines(ogkr);

        var cnt = 0;

        var creator = "";
        var bpm     = "";
        var tTotal  = 0;
        var tTap    = 0;
        var tHold   = 0;
        var tSide   = 0;
        var tSHold  = 0;
        var tFlick  = 0;
        var tBell   = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("CREATOR"))
            {
                creator = line.Split("\t", 2)[1];
                cnt++;
            }
            else if (line.StartsWith("BPM_DEF"))
            {
                bpm = line.Split("\t", 2)[1];
                cnt++;
            }
            // T_TOTAL, T_TAP, T_HOLD, T_SIDE, T_SHOLD, T_FLICK, T_BELL
            else if (line.StartsWith("T_TOTAL"))
            {
                tTotal = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_TAP"))
            {
                tTap = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_HOLD"))
            {
                tHold = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_SIDE"))
            {
                tSide = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_SHOLD"))
            {
                tSHold = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_FLICK"))
            {
                tFlick = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }
            else if (line.StartsWith("T_BELL"))
            {
                tBell = int.Parse(line.Split("\t", 2)[1]);
                cnt++;
            }

            if (cnt == 9) break;
        }

        return new Chart(creator, @const, bpm, tTotal, tTap, tHold, tSide, tSHold, tFlick, tBell);
    }

    private static MusicData ParseOngekiXml(string xml)
    {
        var doc = new XmlDocument();
        doc.Load(xml);

        List<Chart?> charts = new();

        var xmlRootPath = Path.GetDirectoryName(xml)!;

        foreach (XmlNode node in doc.SelectNodes("/MusicData/FumenData")![0]!)
        {
            var path = node.SelectSingleNode("FumenFile/path")?.InnerText;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.Join(xmlRootPath, path)))
            {
                charts.Add(null);
                continue;
            }

            var constIntegerPart    = node.SelectSingleNode("FumenConstIntegerPart")!.InnerText;
            var constFractionalPart = node.SelectSingleNode("FumenConstFractionalPart")!.InnerText;

            var constValue = decimal.Parse($"{constIntegerPart}.{constFractionalPart}");

            charts.Add(ParseOngekiChart(Path.Join(xmlRootPath, path!), constValue));
        }

        return new MusicData(
            int.Parse(doc.SelectSingleNode("/MusicData/Name/id")!.InnerText),
            doc.SelectSingleNode("/MusicData/Name/str")!.InnerText,
            doc.SelectSingleNode("/MusicData/ArtistName/str")!.InnerText,
            doc.SelectSingleNode("/MusicData/MusicSourceName/str")!.InnerText,
            doc.SelectSingleNode("/MusicData/Genre/str")!.InnerText,
            doc.SelectSingleNode("/MusicData/BossCard/str")!.InnerText,
            int.Parse(doc.SelectSingleNode("/MusicData/BossLevel")!.InnerText),
            doc.SelectSingleNode("/MusicData/VersionID/str")!.InnerText,
            doc.SelectSingleNode("/MusicData/ReleaseVersion")!.InnerText,
            doc.SelectSingleNode("/MusicData/MusicRightsName/str")!.InnerText,
            charts
        );
    }

    public static void Main(string[] args)
    {
        var json = new List<MusicData>();

        // foreach (var xml in Directory.GetFiles(Root, "Music.xml", SearchOption.AllDirectories))
        Parallel.ForEach(Directory.GetFiles(Root, "Music.xml", SearchOption.AllDirectories), xml =>
            {
                var data     = ParseOngekiXml(xml);
                var coverSrc = Path.Join(CoverRoot, $"UI_Jacket_{data.Id:0000}.png");
                var coverDst = Path.Join(OutputPath, "cover", $"{data.Id}.png");

                if (!File.Exists(coverDst))
                {
                    if (!File.Exists(coverSrc))
                    {
                        Console.WriteLine($"Cover for {data.Id} not found!");
                    }
                    else
                    {
                        File.Copy(coverSrc, coverDst, true);
                    }
                }

                json.Add(data);
            }
        );

        json = json.OrderBy(x => x.Id).ToList();

        File.WriteAllText(Path.Join(OutputPath, "ongeki.json"), JsonConvert.SerializeObject(json, Formatting.Indented));
    }
}