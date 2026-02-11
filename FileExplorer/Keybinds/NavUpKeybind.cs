using InputLib;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class NavUpKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.Menu.SelectedIndex == 0 && e.Continuous)
        {
            return;
        }

        if (_context.Listener.IsKeyDown(Key.LeftShift))
        {
            _context.Listener.RepeatIntervalMs = 20;
            _context.Listener.RepeatDelayMs = 0;
        }
        
        _context.Menu.MoveSelected(-1);
        _context.RedrawMenu();
    }

    public override void OnKeyUp()
    {
        _context.Listener.RepeatIntervalMs = 30;
        _context.Listener.RepeatDelayMs = 250;
    }
}