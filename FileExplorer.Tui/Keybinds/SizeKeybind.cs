using FileExplorer.Tui.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class SizeKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            _context.ShowFileSizes = !_context.ShowFileSizes;
            Logger.LogI($"Toggled item sizes: {_context.ShowFileSizes}");
            
            lock (_context.OutLock)
            {
                Console.Clear();
                _context.RedrawMenu();
            }
        }
    }
}