using TuiLib;
using FileExplorer.Context;
using LoggerLib;

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
                text = _context.Input($"{Color.Reset.ToAnsi()} Directory name: ");
                            
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
                Logger.LogI("Created new directory");
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