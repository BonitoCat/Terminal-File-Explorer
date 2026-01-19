using InputLib;

namespace FileExplorer.Keybinds;

public class ExitKeybind(MenuContext context, InputListener listener) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.OutLock)
        {
            Console.CursorVisible = true;
            Console.Clear();
            
            InputListener.EnableEcho();
            listener.Dispose();
            listener.WaitForDispose();
            
            context.ExitEvent.Set();
        }
    }
}