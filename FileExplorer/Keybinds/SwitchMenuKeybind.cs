using FileExplorer.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class SwitchMenuKeybind(MenuContext context, int dir, Action<int> callback) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Logger.LogI("Menu switch requested");
        callback.Invoke(dir);
    }
}