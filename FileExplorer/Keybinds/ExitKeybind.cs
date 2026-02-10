using InputLib;

namespace FileExplorer.Keybinds;

public class ExitKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.OutLock)
        {
            Console.CursorVisible = true;
            Console.Clear();
            
            InputListener.EnableEcho();
            _context.Listener?.Dispose();
            _context.Listener?.WaitForDispose();
            
            _context.ExitEvent.Set();
        }
    }
}