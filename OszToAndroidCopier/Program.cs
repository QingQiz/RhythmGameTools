// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

const string adbPath = @" C:\Users\sofee\Desktop\AlasApp_0.4.6_fullcn\AzurLaneAutoScript\toolkit\Lib\site-packages\adbutils\binaries\adb.exe";

var all = Directory.GetFiles(@"O:\extract7k", "*.osz", SearchOption.TopDirectoryOnly);
var cnt = all.Length;
var lck = new object();

Parallel.ForEach(all, f =>
{
    // adb push to android
    var adbPush = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName               = adbPath,
            Arguments              = $"push \"{f}\" /storage/emulated/0/Android/data/me.mugzone.emiria/files/chart/",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            CreateNoWindow         = true
        }
    };
    adbPush.Start();
    adbPush.WaitForExit();

    lock (lck)
    {
        cnt--;
        Console.WriteLine($"{cnt}/{all.Length}");
    }
});