using InputLib;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class NavDownKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (_context.Menu.SelectedIndex == _context.Menu.GetItemCount() - 1 && e.Continuous)
        {
            return;
        }
        
        if (_context.listener.IsKeyDown(Key.LeftShift))
        {
            _context.listener.RepeatRateMs = 20;
            _context.listener.RepeatDelayMs = 0;
        }
        
        _context.Menu.MoveSelected(1);
        _context.RedrawMenu();
    }
    
    public override void OnKeyUp()
    {
        _context.listener.RepeatRateMs = 30;
        _context.listener.RepeatDelayMs = 250;
    }
}