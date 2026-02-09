using System.Text;
using CmdMenu;
using CmdMenu.Controls;

namespace FileExplorer.Keybinds;

public class RenameKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            CmdListBoxItem<CmdLabel>? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
            if (item == null || item.Item.Text == "..")
            {
                return;
            }
            
            Console.CursorVisible = true;
            string? name = _context.Input($"{Color.Reset.ToAnsi()} Rename to: ", _context.StripAnsi(item.Item.Text))?.Trim();
            
            if (name == null || name == _context.StripAnsi(item.Item.Text))
            {
                Console.CursorVisible = false;
                Console.Clear();
                _context.RedrawMenu();
                
                return;
            }
            
            char[] invalidNameChars = Path.GetInvalidFileNameChars();
            
            if (Encoding.Latin1.GetByteCount(name) != name.Length ||
                name?.ToCharArray().Any(c => invalidNameChars.Contains(c)) == true ||
                _context.Menu.GetItemsClone()
                        .Select(item => item.Item.Text)
                        .Contains(name) || name == "..")
            {
                Console.CursorVisible = false;
                Console.Clear();
                
                return;
            }

            string? input = "";
            lock (_context.OutLock)
            {
                while (input != null && input != "y" && input != "n")
                {
                    input = _context.Input($"\x1b[2K{Color.Reset.ToAnsi()} Are you sure? [Y/n]: ", enterNull: true, escapeNo: true)?.Trim().ToLower();
                }

                if (input == "n")
                {
                    Console.CursorVisible = false;
                    Console.Clear();
                    _context.RedrawMenu();
                
                    return;
                }   
            }
            
            try
            {
                if (Directory.Exists(item?.Item.Text))
                {
                    Directory.Move(item.Item.Text, name);
                    
                    List<string> dirHistoryList = _context.DirHistory.ToList();
                    _context.DirHistory.Clear();
                    
                    for (int i = dirHistoryList.Count - 1; i >= 0; i--)
                    {
                        string dirPath = dirHistoryList[i];
                        if (dirPath == Path.GetFullPath(item.Item.Text))
                        {
                            dirPath = Path.GetFullPath(name);
                        }
                        
                        _context.DirHistory.Push(dirPath);
                    }
                }
                else if (File.Exists(item?.Item.Text))
                {
                    File.Move(item.Item.Text, name);
                }
                
                item.Item.Text = name;
            }
            catch { }
            
            Console.CursorVisible = false;
            Console.Clear();
        }
        
        _context.RedrawMenu();
    }
}