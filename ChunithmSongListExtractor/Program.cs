using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using QuickType;
using File = System.IO.File;
using Formatting = Newtonsoft.Json.Formatting;

namespace ChunithmSongListExtractor;

public static class Program
{
    public record Song(long Id, string Title, string Artist, string Genre, string Version, List<Chart> Beatmaps)
    {
        public override string ToString()
        {
            return
                $"{{ Id = {Id}, Title = {Title}, Artist = {Artist}, Genre = {Genre}, Version = {Version}, Beatmaps = {Beatmaps} }}";
        }
    }

    public record Chart(string LevelName, long MaxCombo, string LevelStr, double Constant, string Charter, string Bpm, string ChartName)
    {
        public override string ToString()
        {
            return $"{{ LevelName = {LevelName}, Constant = {Constant}, Charter = {Charter}, Bpm = {Bpm}, ChartName = {ChartName} }}";
        }
    }

    public static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("参数（带空格的文件夹用双引号包起来）：输入文件夹 输出文件夹 /path/to/ffmpeg.exe");
            return;
        }

        var p      = args[0];
        var output = args[1];
        var ffmpeg = args[2];

        if (!Directory.Exists(p))
        {
            Console.WriteLine("输入文件夹不存在: " + p);
            return;
        }

        if (!Directory.Exists(output))
        {
            Console.WriteLine("输出文件夹不存在：" + output);
            return;
        }

        if (!File.Exists(ffmpeg))
        {
            Console.WriteLine("ffmpeg.exe不存在：" + ffmpeg);
            return;
        }

        Directory.CreateDirectory(Path.Join(output, "cover"));
        Directory.CreateDirectory(Path.Join(output, "chart"));

        var versionName = new Dictionary<string, string>
        {
            { "v1 1.00.00", "CHUNITHM" },
            { "v1 1.05.00", "CHUNITHM PLUS" },
            { "v1 1.10.00", "CHUNITHM AIR" },
            { "v1 1.15.00", "CHUNITHM AIR PLUS" },
            { "v1 1.20.00", "CHUNITHM STAR" },
            { "v1 1.25.00", "CHUNITHM STAR PLUS" },
            { "v1 1.30.00", "CHUNITHM AMAZON" },
            { "v1 1.35.00", "CHUNITHM AMAZON PLUS" },
            { "v1 1.40.00", "CHUNITHM CRYSTAL" },
            { "v1 1.45.00", "CHUNITHM CRYSTAL PLUS" },
            { "v1 1.50.00", "CHUNITHM PARADISE" },
            { "v1 1.55.00", "CHUNITHM PARADISE LOST" },
            { "v2 2.00.00", "CHUNITHM NEW!!" },
            { "v2 2.05.00", "CHUNITHM NEW PLUS!!" },
            { "v2 2.10.00", "CHUNITHM SUN" },
            { "v2 2.15.00", "CHUNITHM SUN PLUS" },
            { "v2 2.20.00", "CHUNITHM LUMINOUS" },
            { "v2 2.25.00", "CHUNITHM LUMINOUS PLUS" },
            { "v2 2.30.00", "CHUNITHM VERSE" },
            { "v2 2.35.00", "CHUNITHM VERSE PLUS" }
        };

        var list = new Dictionary<long, Song>();

        Parallel.ForEach(Directory.GetFiles(p, "Music.xml", SearchOption.AllDirectories), i =>
        {
            var path = Path.GetDirectoryName(i)!;
            var xml  = new XmlDocument();
            xml.Load(i);

            MusicData obj;

            try
            {
                var json = JsonConvert.SerializeXmlNode(xml.LastChild, Formatting.Indented);
                obj = ChunithmMusicData.FromJson(json).Data;
            }
            catch
            {
                Console.WriteLine(path);
                throw;
            }

            var beatmaps = new List<Chart>();

            var write = new Song(
                obj.CueFileName.Id,
                obj.Name.Str,
                obj.ArtistName.Str,
                obj.GenreNames.List.StringId.Str,
                versionName[obj.ReleaseTagName.Str],
                beatmaps
            );

            var coverName = $"{write.Id}.png";

            foreach (var (idx, chart) in obj.Fumens.MusicFumenData.Select((d, idx) => (idx, d)))
            {
                if (!chart.Enable) continue;

                if (!File.Exists(Path.Join(path, chart.File.Path))) continue;

                var text = File.ReadAllText(Path.Join(path, chart.File.Path));
                var chartName = Path.GetFileNameWithoutExtension(chart.File.Path);

                var charter = new Regex(@"CREATOR\t(.*)").Match(text).Groups[1].Value.Trim();
                var bpm     = new Regex(@"BPM_DEF\t(.*)").Match(text).Groups[1].Value.Trim();
                var combo   = new Regex(@"T_JUDGE_ALL\t(.*)").Match(text).Groups[1].Value.Trim();
                var bpmList = bpm.Split('\t').Select(double.Parse).ToList();
                var bpmMax  = bpmList.Max();
                var bpmMin  = bpmList.Min();
                bpm = Math.Abs(bpmMax - bpmMin) < 0.01
                    ? bpmMax.ToString("F2")
                    : $"{bpmList.First()} ({bpmMin:F2} - {bpmMax:F2})";

                _ = int.TryParse(combo, out var maxCombo);

                try
                {
                    beatmaps.Add(obj.WorldsEndTagName.Id == -1
                        ? new Chart(chart.Type.Data, maxCombo, $"{chart.Level}{(chart.LevelDecimal >= 50 ? "+" : "")}",
                            chart.Level + chart.LevelDecimal / 100.0, charter, bpm, chartName)
                        : new Chart(chart.Type.Data, maxCombo, obj.WorldsEndTagName.Str,
                            chart.Level + chart.LevelDecimal / 100.0, charter, bpm, chartName));
                }
                catch
                {
                    Console.WriteLine(path);
                    throw;
                }

                File.Copy(Path.Join(path, chart.File.Path), Path.Join(output, "chart", chart.File.Path), true);
            }

            if (!File.Exists(Path.Join(path, coverName)))
            {
                var process = new Process
                {
                    EnableRaisingEvents = true
                };
                process.StartInfo.FileName               = ffmpeg;
                process.StartInfo.UseShellExecute        = false;
                process.StartInfo.CreateNoWindow         = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.Arguments =
                    $"-i \"{Path.Join(path, obj.JaketFile.Path)}\" -loglevel error -stats \"{Path.Join(path, coverName)}\"";
                process.Start();
                process.WaitForExit();
            }

            lock (list)
            {
                if (!list.TryAdd(write.Id, write))
                {
                    if (write.Beatmaps.All(x => x.LevelName != "WORLD'S END"))
                    {
                        list[write.Id] = list[write.Id] with
                        {
                            Version = write.Version,
                            Genre = write.Genre,
                        };
                    }

                    list[write.Id].Beatmaps.AddRange(write.Beatmaps);
                }
            }

            if (!File.Exists(Path.Join(output, "cover", coverName)))
            {
                File.Copy(Path.Join(path, coverName), Path.Join(output, "cover", coverName), false);
            }
        });

        var levelOrder = new Dictionary<string, int>
        {
            { "BASIC", 0 },
            { "ADVANCED", 1 },
            { "EXPERT", 2 },
            { "MASTER", 3 },
            { "ULTIMA", 4 },
            { "WORLD'S END", 5 }
        };

        var res = list.Values
            .OrderBy(x => x.Id)
            .Select(x => x with
            {
                Beatmaps = x.Beatmaps
                    .OrderBy(y => levelOrder[y.LevelName])
                    .ThenBy(y => y.LevelStr)
                    .ThenBy(y => y.MaxCombo)
                    .ToList(),
            }).ToList();

        File.WriteAllText(Path.Join(output, "SongInfo.json"), JsonConvert.SerializeObject(res, Formatting.Indented));
    }
}