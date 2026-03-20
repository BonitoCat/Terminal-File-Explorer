using FileExplorer.Tui.Context;
using TuiLib;

namespace FileExplorer.Tui.Keybinds;

public class PrivilegedKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        if (Environment.IsPrivilegedProcess)
        {
            return;
        }
        
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
        lock (_context.OutLock)
        {
            Window window = new();
            WindowManager.Instance.Add(window);
            
            window.Launch($"bash -c \"sudo \"{Environment.ProcessPath}\"\"");
        }
    }
}