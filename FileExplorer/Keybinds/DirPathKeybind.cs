using CmdMenu;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class DirPathKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyJustPressed()
    {
        lock (_context.Menu.Lock)
        {
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
            
            _context.RefreshItems();
        }
    }
}