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
                Console.Write($"{Color.Reset.ToAnsi()} Search: ");
                search = _context.ReadLine(true);
            
                Console.CursorVisible = false;
                if (search == null)
                {
                    Console.Clear();
                    return;
                }
            }
            
            _context.SearchString = search;
            Task.Run(() =>
            {
                _context.Menu.ViewIndex = 0;
                _context.Menu.SelectedIndex = 0;
                
                Console.Clear();
                _context.Menu.ClearItems();
                
                _context.RefreshItems();
            });
        }
    }
}