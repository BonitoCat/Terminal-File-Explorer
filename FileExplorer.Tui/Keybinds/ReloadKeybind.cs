using FileExplorer.Tui.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class ReloadKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Logger.LogI("Menu reload requested");
        
        Console.Clear();
        _context.RefreshItems();
    }
}