using System.Diagnostics;

namespace FileExplorer.Keybinds;

public class CmdKeybind(MenuContext context) : Keybind(context)
{
    private static Process? _commandLine;

    public override void OnKeyUp()
    {
        OpenCommandLine();
    }
    
    private void OpenCommandLine()
    {
        Console.CursorVisible = true;
        
        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        _commandLine = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            }
        };
        
        lock (_context.Menu.Lock)
        {
            _commandLine.Start();
        }
        
        _commandLine.WaitForExit();
        _commandLine = null;
        
        Console.CursorVisible = false;
        Task.Run(() =>
        {
            _context.RefreshItems();
        });
    }
}