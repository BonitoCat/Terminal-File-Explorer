using System.Diagnostics;
using FileExplorer.Tui.Context;
using InputLib;
using LoggerLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.FileTypes;

public class TextFile
{
    public static void OnClick(IEntryContext context, TuiLabel sender)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
        context.Listener.ConsumeNextKeyUp(Key.LeftCtrl);
        //context.Listener.WaitForKeyInput();
        context.Listener.RaiseEvents = false;
        
        int lines = Console.WindowHeight;
        int columns = Console.WindowWidth;
        Console.WriteLine($"\x1b[8;{Math.Max(lines, 45)};{Math.Max(Console.WindowWidth, 80)}t");
        
        Logger.LogI("Opened file in nano");
        
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "nano",
                Arguments = $"\"{sender.Text}\"",
                UseShellExecute = false,
            },
        };
        
        proc.Start();
        
        lock (context.Menu.Lock)
        {
            proc.WaitForExit();
        }

        lock (context.OutLock)
        {
            Console.CursorVisible = false;
            Console.WriteLine($"\x1b[8;{lines};{columns}t");
        }
        
        Logger.LogI("Closed file in nano");
        
        context.Listener.RaiseEvents = true;
        context.RedrawMenu();
    }
}