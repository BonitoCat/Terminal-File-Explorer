using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using FileExplorer.Context;
using InputLib;
using InputLib.EventArgs;
using LoggerLib;

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
        _context.Listener.RaiseEvents = false;
        
        _context.DisableDrawing();
        InputListener.EnableEcho();
        
        lock (_context.OutLock)
        {
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
                    CreateNoWindow = true,
                },
            };

            Logger.LogI("Opened command line");
            
            _context.CommandLine.Start();
            Thread.Sleep(500);
            
            _context.CommandLine.WaitForExit();
            _context.CommandLine = null;

            Logger.LogI("Closed command line");
            
            Console.CursorVisible = false;
            Console.Clear();
        }

        Thread.Sleep(100);
        _context.Listener.PauseListening = false;
        _context.Listener.RaiseEvents = true;
        
        InputListener.DisableEcho();
        _context.EnableDrawing();
        
        _context.RefreshItems();
    }
}