using System.Diagnostics;
using FileExplorer.Context;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class CopyPathKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (OperatingSystem.IsLinux())
        {
            Clipboard.Copy(Directory.GetCurrentDirectory());
        }
    }
}