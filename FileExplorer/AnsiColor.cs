namespace FileExplorer;

public static class AnsiColor
{
    public const string White = "\x1b[38;2;255;255;255m";
    public const string Orange = "\x1b[38;2;250;130;50m";
    public const string Green = "\x1b[38;2;130;250;50m";
    public const string Blue = "\x1b[38;2;132;180;250m";
    public const string Gray = "\x1b[38;2;120;120;120m";
    public const string Reset = "\x1b[0m";

    public static string FromRgb(byte red, byte green, byte blue)
    {
        return $"\x1b[38;2;{red};{green};{blue}m";
    }
}