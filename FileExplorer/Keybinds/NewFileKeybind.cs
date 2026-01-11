using CmdMenu;

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
                Console.Write($"{Color.Reset.ToAnsi()} File name: ");
                text = _context.ReadLine();
            
                Console.CursorVisible = false;
                if (text == null)
                {
                    Console.Clear();
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
            Task.Run(() =>
            {
                _context.RefreshItems();
                _context.Menu.SelectedIndex = _context.Menu.IndexOf(_context.Menu.GetItemByText(text));
            });
        }
    }
}