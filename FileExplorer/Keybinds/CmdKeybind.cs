using System.Diagnostics;
using InputLib;

namespace FileExplorer.Keybinds;

public class CmdKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        OpenCommandLine();
    }
    
    private void OpenCommandLine()
    {
        InputListener.EnableEcho();
        _context.listener?.StopListening();
        _context.listener?.WaitForClose();
        
        Thread.Sleep(100);
        
        Console.CursorVisible = true;
        
        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        _context.CommandLine = new()
        {
            StartInfo = new()
            {
                FileName = shell,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            }
        };
        
        lock (_context.Menu.Lock)
        {
            _context.CommandLine.Start();
        }
        
        _context.CommandLine.WaitForExit();
        _context.CommandLine = null;
        
        Console.CursorVisible = false;
        Console.Clear();
        Task.Run(() =>
        {
            _context.RefreshItems();
        });
        
        InputListener.DisableEcho();
        
        Thread.Sleep(100);
        _context.listener?.StartListening();
    }
}