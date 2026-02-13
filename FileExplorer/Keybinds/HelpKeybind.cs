using FileExplorer.Context;
using InputLib;

namespace FileExplorer.Keybinds;

public class HelpKeybind(MenuContext context, string helpString) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            Console.Clear();
            Console.WriteLine(helpString);
            Thread.Sleep(500);
            
            _context.Listener.WaitForKeyInput(Key.Escape);
            Console.Clear();
            Thread.Sleep(50);
            
            _context.RedrawMenu();
        }
    }
}