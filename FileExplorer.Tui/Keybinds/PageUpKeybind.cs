using FileExplorer.Tui.Context;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class PageUpKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        int prevViewIndex = _context.Menu.ViewIndex;

        _context.Menu.ViewIndex = Math.Max(_context.Menu.ViewIndex - _context.Menu.ViewRange, 0);
        _context.Menu.SelectedIndex += _context.Menu.ViewIndex - prevViewIndex;

        int relativeIndex = _context.Menu.SelectedIndex - _context.Menu.ViewIndex;
        int maxRelativeIndex = _context.Menu.ViewRange - 1 - _context.Menu.ScrollOvershoot;

        if (relativeIndex > maxRelativeIndex)
        {
            _context.Menu.ViewIndex = Math.Min(
                _context.Menu.SelectedIndex - maxRelativeIndex,
                Math.Max(_context.Menu.GetItemCount() - _context.Menu.ViewRange, 0));
        }

        _context.RedrawMenu();
    }
}