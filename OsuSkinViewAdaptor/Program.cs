using System.Runtime.CompilerServices;

namespace OsuSkinViewAdaptor;

internal static class CurrentConfig
{
    // inch
    public const int Size = 27;

    // 16:9
    public const int Width = 16;
    public const int Height = 9;

    // column width
    public const int ColumnWidth = 45;

    // width for note height scale
    public const int NoteHeight = 55;

    // hit position
    public const int HitPosition = 440;

    // score position
    public const int ScorePosition = 250;

    // speed
    public const int Speed = 33;
}

public static class Program
{
    private static int Square(this int value) => value * value;
    private static double SquareRoot(this double value) => Math.Sqrt(value);

    private static (double wInch, double hInch) GetMonitorSize(int inch, int w, int h)
    {
        var x = (
            (double)inch.Square() / (w.Square() + h.Square())
        ).SquareRoot();

        return (w * x, h * x);
    }

    public static void Main(string[] args)
    {
        var inch = 32;
        // 16:9
        var res = new[] { 16, 9 };

        var (wCurrent, hCurrent) = GetMonitorSize(CurrentConfig.Size, CurrentConfig.Width, CurrentConfig.Height);
        var (wNew, hNew)         = GetMonitorSize(inch, res[0], res[1]);

        var hScale = hCurrent / hNew;

        // note width in inch
        var noteWidth = wCurrent * CurrentConfig.ColumnWidth / (480.0 / CurrentConfig.Height * CurrentConfig.Width);

        var columnWidth = (int)Math.Round(noteWidth / wNew * (480.0 / res[1] * res[0]));
        //
        var columnStart = (int)Math.Round((480.0 / res[1] * res[0] - 7 * columnWidth) / 2);
        //
        var noteHeight    = (int)Math.Round(CurrentConfig.NoteHeight    * hScale);
        var hitPosition   = (int)Math.Round(CurrentConfig.HitPosition   * hScale);
        var speed         = (int)Math.Round(CurrentConfig.Speed         * hScale);
        var scorePosition = (int)Math.Round(CurrentConfig.ScorePosition * hScale);


        Console.WriteLine("ColumnWidth: "   + columnWidth);
        Console.WriteLine("ColumnStart: "   + columnStart);
        Console.WriteLine("NoteHeight: "    + noteHeight);
        Console.WriteLine("HitPosition: "   + hitPosition);
        Console.WriteLine("ScorePosition: " + scorePosition);
        Console.WriteLine("Speed: "         + speed);
        Console.WriteLine("====================================");
        // centered play field
        Console.WriteLine("ColumnStart: {0}", columnStart);
        Console.WriteLine("------------------------------------");
        Console.WriteLine($"HitPosition: {hitPosition}");
        Console.WriteLine($"LightPosition: {hitPosition}");
        Console.WriteLine($"ScorePosition: {scorePosition}");
        Console.WriteLine("------------------------------------");
        Console.WriteLine("ColumnWidth: {0},{0},{0},{0},{0},{0},{0}", columnWidth);
        Console.WriteLine("WidthForNoteHeightScale: {0}", noteHeight);
        Console.WriteLine("------------------------------------");
        Console.WriteLine("Speed: {0}", speed);
    }
}