using FileExplorer.Tui.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class DirHistoryKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.DirHistory.Count > 0)
        {
            Logger.LogI("Popped directory history");
            _context.OnClickDir(new(_context.DirHistory.Pop()), false);
        }
    }
}