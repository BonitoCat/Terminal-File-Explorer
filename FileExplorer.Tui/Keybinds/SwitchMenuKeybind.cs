using FileExplorer.Tui.Context;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class SwitchMenuKeybind(IEntryContext context, int dir, Action<int> callback) : Keybind(context)
{
    public override void OnKeyJustPressed()
    {
        Logger.LogI("Menu switch requested");
        callback.Invoke(dir);
    }
}