using FileExplorer.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class ReloadKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Logger.LogI("Menu reload requested");
        
        Console.Clear();
        _context.RefreshItems();
    }
}