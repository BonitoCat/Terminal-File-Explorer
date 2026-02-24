using TuiLib;
using FileExplorer.Context;
using InputLib;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class SearchKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        Logger.LogI("Search requested");
        lock (_context.Menu.Lock)
        {
            string? search = "";
            lock (_context.OutLock)
            {
                Console.CursorVisible = true;
                
                _context.Listener.ConsumeNextKeyDown(Key.F);
                _context.Listener.ConsumeNextKeyUp(Key.F);
                
                Logger.LogI("Reading search input...");
                search = _context.Input($"{Color.Reset.ToAnsi()} Search: ", enterNull: true);
            
                Console.CursorVisible = false;
                if (search == null)
                {
                    Console.Clear();
                    _context.RedrawMenu();
                    
                    Logger.LogI("Search canceled");
                    
                    return;
                }
            }

            Logger.LogI("Read search input");
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