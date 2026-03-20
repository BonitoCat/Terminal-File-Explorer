using FileExplorer.Tui.Context;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class CopyPathKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (OperatingSystem.IsLinux())
        {
            Clipboard.Copy(Directory.GetCurrentDirectory());
        }
    }
}