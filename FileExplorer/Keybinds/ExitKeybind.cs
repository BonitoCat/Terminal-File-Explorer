using InputLib;

namespace FileExplorer.Keybinds;

public class ExitKeybind(MenuContext context, InputListener listener) : Keybind(context)
{
    public override void OnKeyUp()
    {
        _context.RefreshCancelSource.Cancel();
        
        lock (_context.OutLock)
        {
            Console.CursorVisible = true;
            Console.Clear();
            
            InputListener.EnableEcho();
            listener.StopListening();
            listener.WaitForClose();
            
            Environment.Exit(0);
        }
    }
}