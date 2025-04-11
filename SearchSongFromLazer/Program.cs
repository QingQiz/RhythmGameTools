using System.Diagnostics;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using Realms;

namespace SearchSongFromLazer;

class Program
{
    private const string LazerPath = @"O:\GameStorage\osu!lazer";

    static void Main(string[] args)
    {
        Console.Write("Title: ");
        var title = Console.ReadLine()!;

        var fp    = Path.Join(LazerPath, "client.realm");
        var fpNew = Path.Join(LazerPath, "client.realm.new");

        File.Copy(fp, fpNew, true);

        using (var realm = Realm.GetInstance(new RealmConfiguration(fpNew) { SchemaVersion = 4700 }))
        {
            var beatmapSets = realm.All<BeatmapSetInfo>().ToList();
            var beatmaps    = beatmapSets.SelectMany(x => x.Beatmaps);
            var matched = beatmaps.Where(x =>
                x.Metadata.Title.Contains(title, StringComparison.OrdinalIgnoreCase)
             || x.Metadata.TitleUnicode.Contains(title, StringComparison.OrdinalIgnoreCase)
             || x.DifficultyName.Contains(title, StringComparison.OrdinalIgnoreCase)
            );

            // audio file path -> .osu
            var dict = new Dictionary<string, string>();
            foreach (var beatmap in matched)
            {
                var file = beatmap.BeatmapSet?.Files.FirstOrDefault(f => f.Filename == beatmap.Metadata.AudioFile);
                if (file != null)
                {
                    dict[file.File.GetStoragePath()] = BuildAudioName(beatmap);
                }
            }
            var l = dict.ToList();
            //print dict
            var i = 0;
            foreach (var (key, value) in l)
            {
                Console.WriteLine($"{i++}. {Path.Join(LazerPath, "files", key)} -> {value}");
            }

            while (true)
            {
                Console.Write("选一个：");
                var res = int.TryParse(Console.ReadLine()!, out i);
                if (!res) continue;

                File.Copy(Path.Join(LazerPath, "files", l[i].Key), Path.Join(LazerPath, l[i].Value), true);
                Process.Start("explorer.exe", $"/e, /select, \"{Path.Join(LazerPath, l[i].Value)}\"");
                break;
            }
        }
        File.Delete(fpNew);
        File.Delete(fpNew + ".lock");
    }

    private static string BuildAudioName(BeatmapInfo beatmap)
    {
        var title  = beatmap.Metadata.TitleUnicode;
        var author = beatmap.Metadata.ArtistUnicode;

        if (string.IsNullOrWhiteSpace(title)) title   = beatmap.Metadata.Title;
        if (string.IsNullOrWhiteSpace(author)) author = beatmap.Metadata.Artist;

        return $"{author} - {title} ---- {beatmap.DifficultyName}.{beatmap.Metadata.AudioFile.Split('.').Last()}";
        ;
    }
}