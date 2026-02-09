using System.Diagnostics;
using InputLib;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class CmdKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (e.Continuous)
        {
            return;
        }
        
        OpenCommandLine();
    }
    
    private void OpenCommandLine()
    {
        _context.Listener.PauseListening = true;
        _context.DisableDrawing();
        
        InputListener.EnableEcho();
        Console.CursorVisible = true;

        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        _context.CommandLine = new()
        {
            StartInfo = new()
            {
                FileName = shell,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            },
        };
        
        _context.CommandLine.Start();
        _context.CommandLine.WaitForExit();
        _context.CommandLine = null;
        
        Console.CursorVisible = false;
        lock (_context.OutLock)
        {
            Console.Clear();
        }
        
        _context.RefreshItems();

        Thread.Sleep(100);
        
        InputListener.DisableEcho();
        _context.Listener.PauseListening = false;
        _context.EnableDrawing();
        
        _context.Listener.ConsumeNextKeyDown(Key.D);
    }
}