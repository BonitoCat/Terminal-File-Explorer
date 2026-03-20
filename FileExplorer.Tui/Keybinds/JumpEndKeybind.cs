using FileExplorer.Tui.Context;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class JumpEndKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            _context.Menu.SelectedIndex = _context.Menu.GetItemCount() - 1;
            _context.Menu.ViewIndex = Math.Max(_context.Menu.GetItemCount() - _context.Menu.ViewRange, 0);
            _context.RedrawMenu();
        }
    }
}