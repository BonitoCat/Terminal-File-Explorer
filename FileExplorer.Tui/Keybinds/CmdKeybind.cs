using FileExplorer.Tui.Context;
using InputLib;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class CmdKeybind(IEntryContext context) : Keybind(context)
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
            Console.SetCursorPosition(0, _context.Menu.MaxHeight + 2);
            Console.CursorVisible = true;
            
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

            Logger.LogI($"IsInputRedirected={Console.IsInputRedirected}, IsOutputRedirected={Console.IsOutputRedirected}");
            Logger.LogI("Opened command line");
            
            _context.CommandLine.Start();
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