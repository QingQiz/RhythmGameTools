using System.Text;

namespace BmsFilenameFixer;

internal class Program
{
    static void Main(string[] args)
    {
        //var toFix = @"F:\workspace\bms\packs\BOFXV\1. BOFXV - THE BMS OF FIGHTERS eXtreme Violence - （No.1~No.149）";
        var toFix = @"F:\workspace\bms\packs\G2R2018";

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var jis = Encoding.GetEncoding("Shift-JIS");
        var enc = Encoding.GetEncoding("GB2312");

        var dirs = Directory.GetDirectories(toFix, "*", SearchOption.AllDirectories);

        foreach (var dir in dirs.Reverse())
        {
            var parent  = Path.GetDirectoryName(dir);
            var oldName = Path.GetFileName(dir);
            var newName = jis.GetString(enc.GetBytes(oldName));

            if (oldName == newName) continue;

            var newPath = Path.Combine(parent, newName);

            Console.WriteLine($"{oldName} -> {newName}");
            Directory.Move(dir, newPath);
        }   

        var files = Directory.GetFiles(toFix, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var parent  = Path.GetDirectoryName(file);
            var oldName = Path.GetFileName(file);
            var newName = jis.GetString(enc.GetBytes(oldName));

            if (oldName == newName) continue;

            var newPath = Path.Combine(parent, newName);

            Console.WriteLine($"{oldName} -> {newName}");
            File.Move(file, newPath);
        }
    }
}