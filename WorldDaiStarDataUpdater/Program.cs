using Newtonsoft.Json.Linq;

class Program
{
    private const string DataRepPath = @"F:\workspace\wdi\sirius-decrypted-data";
    private const string OutputPath = @"C:\Users\sofee\Desktop\workspace\QQBOT\Marisa.Frontend\public\assets\wds";

    private static readonly Dictionary<string, string> DiffMap = new()
    {
        ["1"] = "Normal",
        ["2"] = "Hard",
        ["3"] = "Extra",
        ["4"] = "Stella",
        ["5"] = "Olivier"
    };

    public static string MusicInfoJsonPath = Path.Combine(DataRepPath, "master", "MusicMaster.json");
    public static string ChartPath = Path.Combine(DataRepPath, "music");
    public static string ChartOutputPath = Path.Combine(OutputPath, "chart");
    public static string CoverOutputPath = Path.Combine(OutputPath, "cover");

    private static void Main()
    {
        if (!SetupDataRepository()) return;

        if (!File.Exists(MusicInfoJsonPath))
        {
            Console.WriteLine("Music info JSON file not found.");
            return;
        }

        // Ensure output directories exist before parallel processing
        Directory.CreateDirectory(ChartOutputPath);
        Directory.CreateDirectory(CoverOutputPath);

        var musicInfoJsonText = File.ReadAllText(MusicInfoJsonPath);
        var musicList = JArray.Parse(musicInfoJsonText);

        Parallel.ForEach(musicList, CpyMusicFiles);
    }

    private static bool SetupDataRepository()
    {
        if (!File.Exists(DataRepPath))
        {
            var parent = Path.GetDirectoryName(DataRepPath)!;
            Directory.CreateDirectory(parent);

            var process = System.Diagnostics.Process.Start("cmd.exe",
                $"/C git clone https://github.com/SonolusHaniwa/sirius-decrypted-data {DataRepPath} --depth 1");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine("Failed to clone the data repository.");
                return false;
            }
        }
        else
        {
            var process = System.Diagnostics.Process.Start("cmd.exe", $"/C cd {DataRepPath} && git pull");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine("Failed to update the data repository.");
                return false;
            }
        }

        return true;
    }

    private static void CpyMusicFiles(JToken music)
    {
        var musicTitle = music["Name"]!.ToObject<string>()!;
        var musicId = music["Id"]!.ToObject<int>()!;

        var musicChartPath = Path.Join(ChartPath, musicId.ToString());

        if (!Directory.Exists(musicChartPath))
        {
            Console.WriteLine($"Chart for `{musicTitle}' does not exist.");
            return;
        }

        foreach (var chart in Directory.GetFiles(musicChartPath, "*.txt"))
        {
            var chartFileName = Path.GetFileName(chart).Split(".")[0];
            if (DiffMap.TryGetValue(chartFileName, out var diff))
            {
                var outFn = $"{musicId}_{diff}.txt";
                File.Copy(chart, Path.Combine(ChartOutputPath, outFn), true);
            }
        }

        var cover = Path.Combine(musicChartPath, "cover.png");
        if (File.Exists(cover))
        {
            var outFn = $"{musicId}.png";
            var outPath = Path.Combine(CoverOutputPath, outFn);
            if (!File.Exists(outPath))
            {
                File.Copy(cover, Path.Combine(CoverOutputPath, outFn), true);
            }
        }
        else
        {
            Console.WriteLine($"Cover for `{musicTitle}' does not exist.");
        }
    }
}