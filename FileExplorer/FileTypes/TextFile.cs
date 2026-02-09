using System.Diagnostics;
using CmdMenu;
using CmdMenu.Controls;
using InputLib;

namespace FileExplorer.FileTypes;

public class TextFile
{
    public static void OnClick(MenuContext context, CmdLabel sender)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
        context.Listener.ConsumeNextKeyUp(Key.LeftCtrl);
        context.Listener.RaiseEvents = false;
        
        int lines = Console.WindowHeight;
        int columns = Console.WindowWidth;
        Console.WriteLine($"\x1b[8;{Math.Max(lines, 45)};{Math.Max(Console.WindowWidth, 80)}t");
        
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
        
        context.Listener.RaiseEvents = true;
        context.RedrawMenu();
    }
}