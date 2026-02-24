using TuiLib;
using FileExplorer.Context;
using LoggerLib;

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
                Logger.LogI("Created new file");
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