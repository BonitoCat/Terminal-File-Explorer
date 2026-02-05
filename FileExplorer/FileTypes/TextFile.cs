using System.Diagnostics;
using CmdMenu;

namespace FileExplorer.FileTypes;

public class TextFile
{
    public static void OnClick(MenuContext context, MenuItem sender)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        
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

        lock (context.Menu.Lock)
        {
            proc.Start();
            proc.WaitForExit();

            Console.CursorVisible = false;
            Console.Clear();
            Console.WriteLine($"\x1b[8;{lines};{columns}t");
            context.RedrawMenu();
        }

        context.Listener.RaiseEvents = true;
    }
}