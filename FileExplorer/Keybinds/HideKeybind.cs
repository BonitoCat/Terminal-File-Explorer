using FileExplorer.Context;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class HideKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            _context.ShowHiddenFiles = !_context.ShowHiddenFiles;
            
            lock (_context.OutLock)
            {
                Console.Clear();
            }
            _context.RefreshItems();
        }
    }
}