using CmdMenu;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class NewFileKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            string? text = "";
            lock (_context.OutLock)
            {
                Console.CursorVisible = true;
                text = _context.Input($"{Color.Reset.ToAnsi()} File name: ");
            
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
                File.Create(text).Close();
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