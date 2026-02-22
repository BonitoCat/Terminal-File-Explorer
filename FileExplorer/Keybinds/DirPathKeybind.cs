using CmdMenu;
using FileExplorer.Context;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class DirPathKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyJustPressed()
    {
        lock (_context.Menu.Lock)
        {
            Logger.LogI("Reading directory path input");
            Console.CursorVisible = true;

            string? input = "";
            lock (_context.OutLock)
            {
                input = _context.Input($"{Color.Reset.ToAnsi()} Path of directory: ");

                if (input == null)
                {
                    Console.CursorVisible = false;
                    Console.Clear();
                    _context.RedrawMenu();
                    
                    Logger.LogI("Canceled directory path input");

                    return;
                }   
            }

            if (Directory.Exists(input))
            {
                _context.DirHistory.Push(Directory.GetCurrentDirectory());
                Directory.SetCurrentDirectory(input);
            }

            Console.CursorVisible = false;
            Console.Clear();
            
            Logger.LogI("Done reading directory path input");
            
            _context.RefreshItems();
        }
    }
}