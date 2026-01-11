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
            Thread.Sleep(1000);
            
            listener.WaitForKeyInput();
            Console.Clear();
        }
    }
}