namespace ArcaeaSongListExtractor;

internal static class Program
{
    private const string ResultPath = @"F:\workspace\arcaea";
    private const string ExtractedApkPath = @"D:\Downloads\Default\moe.low.arc_5.0.5";
    private const string SongPath = $@"{ExtractedApkPath}\assets\songs";
    private const string SongListPath = $@"{SongPath}\songlist";

    private static string GetSongPath(string id)
    {
        return Directory.Exists($@"{SongPath}\dl_{id}") ? $@"{SongPath}\dl_{id}" : $@"{SongPath}\{id}";
    }

    private static void Prepare()
    {
        if (!Directory.Exists(ResultPath))
        {
            Directory.CreateDirectory($@"{ResultPath}\cover");
        }
    }

    private static string GetCoverPath(string songPath, bool byd)
    {
        if (byd)
        {
            var res = $@"{songPath}\base_3.jpg";
            if (File.Exists(res)) return res;

            res = $@"{songPath}\1080_3.jpg";
            if (File.Exists(res)) return res;

            return GetCoverPath(songPath, false);
        }
        else
        {
            var res = $@"{songPath}\base.jpg";

            if (File.Exists(res)) return res;

            res = $@"{songPath}\1080_base.jpg";

            if (File.Exists(res)) return res;
        }

        throw new FileNotFoundException($"{(byd ? "BYD" : "NORM")}: {songPath}");
    }

    private static void CopyCovers(IEnumerable<Song> songs)
    {
        Parallel.ForEach(songs, song =>
        {
            var songPath = GetSongPath(song.Id);
            var from     = GetCoverPath(songPath, false);
            var to       = $@"{ResultPath}\cover\{song.Idx}.jpg";

            Copy(from, to);

            if (song.Difficulties.Length <= 3) return;

            from = GetCoverPath(songPath, true);
            to   = to.Replace(".jpg", "_3.jpg");

            Copy(from, to);
        });
    }

    private static void Copy(string from, string to)
    {
        if (File.Exists(to)) return;
        File.Copy(from, to);
    }

    private static void Main()
    {
        Prepare();

        var songList = ArcaeaSongList.FromJson(File.ReadAllText(SongListPath));

        CopyCovers(songList.Songs);
    }
}