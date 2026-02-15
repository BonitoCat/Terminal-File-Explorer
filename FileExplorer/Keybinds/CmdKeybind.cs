using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using FileExplorer.Context;
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
        Console.SetCursorPosition(0, _context.Menu.MaxHeight + 4);

        string shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        _context.CommandLine = new()
        {
            StartInfo = new()
            {
                FileName = shell,
                Arguments = "-i",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
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
        _context.Listener.PauseListening = false;
        
        InputListener.DisableEcho();
        _context.EnableDrawing();
        
        _context.Listener.ConsumeNextKeyDown(Key.D);
        _context.RedrawMenu();
    }
}