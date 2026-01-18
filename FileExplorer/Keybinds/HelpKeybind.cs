using InputLib;

namespace FileExplorer.Keybinds;

public class HelpKeybind(MenuContext context, InputListener listener, string helpString) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            Console.Clear();
            Console.WriteLine(helpString);
            Thread.Sleep(500);
            
            listener.WaitForKeyInput(Key.Escape);
            Console.Clear();
            Thread.Sleep(50);
            
            _context.RedrawMenu();
        }
    }
}