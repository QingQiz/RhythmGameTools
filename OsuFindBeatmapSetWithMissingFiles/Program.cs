using osu.Game.Extensions;
using OsuApi;

namespace OsuFindBeatmapSetWithMissingFiles;

class Program
{
    private const string LazerPath = @"O:\GameStorage\osu!lazer";

    static void Main(string[] args)
    {
        var res = LazerDbApi.WithAllBeatmapSetInfo(LazerPath, list =>
        {
            var res = new List<(int BeatmapSetId, List<(string Storage, string Filename)> File)>();

            foreach (var bms in list)
            {
                var files   = bms.Files;
                var storage = files.Select(x => x.File.GetStoragePath());
                res.Add((bms.OnlineID, storage.Zip(files.Select(x => x.Filename)).ToList().ToList()));
            }

            return res;
        });

        var filtered = res.AsParallel()
            .Select(x => x with
            {
                File = x.File.Where(f => !File.Exists(f.Storage)).ToList()
            })
            .Where(x => x.File.Count > 0)
            .ToList();

        foreach (var x in filtered)
        {
            Console.WriteLine($"{x.BeatmapSetId} Missing: {string.Join(", ", x.File.Select(f => f.Filename))}");
        }
    }
}