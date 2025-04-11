using osu.Game.Beatmaps;
using Realms;

namespace OsuApi;

public static class LazerDbApi
{
    public static T WithAllBeatmapSetInfo<T>(string lazerPath, Func<List<BeatmapSetInfo>, T> action)
    {
        var fp    = Path.Join(lazerPath, "client.realm");
        var fpNew = Path.Join(lazerPath, "client.realm.new");
        File.Copy(fp, fpNew, true);

        T res;
        using (var realm = Realm.GetInstance(new RealmConfiguration(fpNew) { SchemaVersion = 4700 }))
        {
            var info = realm.All<BeatmapSetInfo>().ToList();
            res = action(info);
        }
        File.Delete(fpNew);
        File.Delete(fpNew + ".lock");

        return res;
    }

    public static void WithAllBeatmapSetInfo(string lazerPath, Action<List<BeatmapSetInfo>> action)
    {
        WithAllBeatmapSetInfo(lazerPath, x =>
        {
            action(x);
            return 0;
        });
    }
}