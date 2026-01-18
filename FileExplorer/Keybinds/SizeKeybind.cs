using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class SizeKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            _context.ShowFileSizes = !_context.ShowFileSizes;
            
            lock (_context.OutLock)
            {
                Console.Clear();
                _context.RedrawMenu();
            }
        }
    }
}