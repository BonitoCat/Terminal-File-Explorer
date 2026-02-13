using FileExplorer.Context;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class DirHistoryKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.DirHistory.Count > 0)
        {
            _context.OnClickDir(new(_context.DirHistory.Pop()), false);
        }
    }
}