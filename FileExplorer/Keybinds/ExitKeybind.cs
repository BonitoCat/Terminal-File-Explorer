using FileExplorer.Context;
using InputLib;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class ExitKeybind(MenuContext context, List<MenuContext> contexts) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.OutLock)
        {
            Logger.LogI("Exit requested");
            
            contexts.ForEach(context =>
            {
                context.RefreshCancelSource.Cancel();
                context.StopRenderLoop();
                
                context.Listener?.Dispose();
                context.Listener?.WaitForDispose();
            });
            
            InputListener.EnableEcho();
            
            Console.CursorVisible = true;
            Console.Clear();
            
            _context.ExitEvent.Set();
        }
    }
}