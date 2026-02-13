namespace FileExplorer.Context;

public class ClipboardContext
{
    public List<string> Items { get; } = new();
    public ClipboardMode? Mode { get; set; } = ClipboardMode.None;
}