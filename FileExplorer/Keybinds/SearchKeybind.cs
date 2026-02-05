using CmdMenu;

namespace FileExplorer.Keybinds;

public class SearchKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            string? search = "";
            lock (_context.OutLock)
            {
                Console.CursorVisible = true;
                search = _context.Input($"{Color.Reset.ToAnsi()} Search: ", enterNull: true);
            
                Console.CursorVisible = false;
                if (search == null)
                {
                    Console.Clear();
                    _context.RedrawMenu();
                    
                    return;
                }
            }

            if (search != _context.SearchString)
            {
                _context.SearchString = search;
                
                _context.RefreshItems();
                _context.Menu.ViewIndex = 0;
                _context.Menu.SelectedIndex = 0;
            }
            
            lock (_context.OutLock)
            {
                Console.Clear();
            }
            _context.RedrawMenu();
        }
    }
}