using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace OsuSkinGen;

public static class Program
{
    private const string TemplatePath = @"O:\GameStorage\osu!lazer\Skin\Template";

    private static string GetVersion()
    {
        var versionFile = Path.Join(TemplatePath, "version.txt");
        var version     = File.Exists(versionFile) ? File.ReadAllText(versionFile) : "0.1";

        var versionMinor = version.Split('.').Length > 1
            ? int.Parse(version.Split('.')[1])
            : 0;
        var versionMajor = version.Split('.').Length > 0
            ? int.Parse(version.Split('.')[0])
            : 0;

        var versionNew = $"{versionMajor}.{versionMinor + 1}";
        File.WriteAllText(versionFile, versionNew);
        return versionNew;
    }

    private static string GetConfigForKeyCount(int keyCount, int monitorW, int monitorH)
    {
        var config = new
        {
            ColumnWidth   = 45,
            HitPosition   = 415,
            ScorePosition = 250,
        };

        var columnStart = (int)Math.Round((480.0 / monitorH * monitorW - keyCount * config.ColumnWidth) / 2);
        // repeat 7 times

        var colW         = string.Join(',', Enumerable.Range(0, keyCount).Select(_ => config.ColumnWidth));
        var laneHitColor = string.Join('\n', Enumerable.Range(0, keyCount).Select(i => $"ColourLight{i + 1}: 0, 0, 0"));
        var laneBgColor  = string.Join('\n', Enumerable.Range(0, keyCount).Select(i => $"Colour{i + 1}: 0, 0, 0"));

        var laneMap = new Dictionary<int, int[]>
        {
            [1]  = [1],
            [2]  = [1, 1],
            [3]  = [1, 3, 1],
            [4]  = [1, 2, 2, 1],
            [5]  = [1, 2, 3, 2, 1],
            [6]  = [1, 2, 1, 1, 2, 1],
            [7]  = [1, 2, 1, 3, 1, 2, 1],
            [8]  = [1, 2, 1, 3, 3, 1, 2, 1],
            [9]  = [1, 2, 1, 2, 3, 2, 1, 2, 1],
            [10] = [1, 2, 1, 2, 3, 3, 2, 1, 2, 1],
        };

        return
            $"""
             Keys:{keyCount}
             ColumnStart: {columnStart}
             //StageBottom: StageBottom

             HitPosition: {config.HitPosition}
             LightPosition: {config.HitPosition}
             ScorePosition:{config.ScorePosition}
             {(keyCount == 6 ? $"ColumnSpacing: 0,0,{config.ColumnWidth},0,0" : "")}

             SpecialStyle: 0
             UpsideDown: 0
             JudgementLine: 1

             LightFramePerSecond: 40
             ColumnWidth: {colW}
             WidthForNoteHeightScale: 55

             BarlineHeight: 1.2
             FontCombo: combo
             ColourBarline: 0,255,0,255

             //Colours
             {laneHitColor}

             {laneBgColor}

             //images
             {GetNoteConf()}

             //Keys
             ColumnLineWidth: 0,1,1,1,1,1,1,0
             ColourColumnLine: 255,255,255,50

             """;

        (string Rice, string LnH, string LnB, string LnT) GetKeyConf(int i)
        {
            return i switch
            {
                1 => ("Note/Note-1H", "Note/Note-1H", "Note/LNBody", "Note/LNTail"),
                2 => ("Note/Note-2H", "Note/Note-2H", "Note/LNBody", "Note/LNTail"),
                3 => ("Note/Note-4H", "Note/Note-4H", "Note/LNBody", "Note/LNTail"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        string GetNoteConf()
        {
            var sb   = new StringBuilder();
            var conf = laneMap[keyCount];
            for (var i = 0; i < conf.Length; i++)
            {
                var img = GetKeyConf(conf[i]);
                sb.AppendLine($"NoteImage{i}:{img.Rice}");
                sb.AppendLine($"NoteImage{i}H:{img.LnH}");
                sb.AppendLine($"NoteImage{i}L:{img.LnB}");
                sb.AppendLine($"NoteImage{i}T:{img.LnT}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public static void Main()
    {
        var v = GetVersion();
        var header =
            $"""
             [General]
             Name: QINGQIZ's Skin v{v}
             Author: QINGQIZ
             Version: latest
             SliderBallFlip: 1
             CursorRotate: 0
             CursorExpand: 0
             CursorCentre: 1
             CursorTrailRotate: 0
             SliderBallFrames: 60
             HitCircleOverlayAboveNumer: 1
             ComboBurstRandom: 0
             SliderStyle: 2
             AnimationFramerate: 60
             AllowSliderBallTint: 1
             SpinnerFadePlayfield: 0

             [Colours]
             Combo1: 80, 115, 255
             Combo2: 230, 110, 150

             SliderBorder: 255,255,255
             SliderTrackOverride: 0,0,0
             SongSelectActiveText: 255,255,255
             SongSelectInactiveText: 106,121,145
             MenuGlow: 8,37,82
             InputOverlayText: 255,255,255

             [Fonts]
             HitCircleOverlap: 0
             // HitCirclePrefix: fonts/hitcircle/default
             // ComboPrefix: fonts/combo/score
             ComboPrefix: blank
             // ScorePrefix: fonts/score/score

             """;

        var sb = new StringBuilder(header);
        for (var k = 1; k <= 10; k++)
        {
            sb.AppendLine("[Mania]");
            sb.AppendLine(GetConfigForKeyCount(k, 16, 9));
        }
        File.WriteAllText(Path.Join(TemplatePath, "skin.ini"), sb.ToString());
        // create zip
        var zipPath = Path.Join(Path.GetDirectoryName(TemplatePath), $"QINGQIZ's Skin v{v}.osk");
        ZipFile.CreateFromDirectory(TemplatePath, zipPath, CompressionLevel.Fastest, false);
        Process.Start("explorer.exe", $"/e, /select, \"{zipPath}\"");
    }
}