using CmdMenu;

namespace FileExplorer.Keybinds;

public class DirPathKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            Console.CursorVisible = true;

            string? input = "";
            lock (_context.OutLock)
            {
                Console.Write($"{Color.Reset.ToAnsi()} Path of directory: ");
                input = _context.ReadLine();

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