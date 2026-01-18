using CmdMenu;

namespace FileExplorer.Keybinds;

public class NewFolderKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            string? text = "";
            lock (_context.OutLock)
            {
                Console.CursorVisible = true;
                Console.Write($"{Color.Reset.ToAnsi()} Directory name: ");
                text = _context.ReadLine();
                            
                Console.CursorVisible = false;
                if (text == null)
                {
                    Console.Clear();
                    _context.RedrawMenu();
                    return;
                }   
            }
                            
            try
            {
                Directory.CreateDirectory(text);
            }
            catch (Exception)
            {
                return;
            }
            
            Console.Clear();
            _context.RefreshItems();
        }
    }
}