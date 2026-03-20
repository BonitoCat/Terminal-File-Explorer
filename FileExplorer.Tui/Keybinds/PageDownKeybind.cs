using FileExplorer.Tui.Context;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class PageDownKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        int itemCount = _context.Menu.GetItemCount();
        int maxViewIndex = Math.Max(itemCount - _context.Menu.ViewRange, 0);
        int previousViewIndex = _context.Menu.ViewIndex;

        _context.Menu.ViewIndex = Math.Min(_context.Menu.ViewIndex + _context.Menu.ViewRange, maxViewIndex);
        _context.Menu.SelectedIndex += _context.Menu.ViewIndex - previousViewIndex;

        int relativeSelectedIndex = _context.Menu.SelectedIndex - _context.Menu.ViewIndex;

        if (relativeSelectedIndex < _context.Menu.ScrollOvershoot)
        {
            _context.Menu.ViewIndex = Math.Max(_context.Menu.SelectedIndex - _context.Menu.ScrollOvershoot, 0);
        }

        _context.RedrawMenu();
    }
}