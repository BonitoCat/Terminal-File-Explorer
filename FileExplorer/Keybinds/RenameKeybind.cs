using System.Text;
using CmdMenu;

namespace FileExplorer.Keybinds;

public class RenameKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            if (_context.Menu.GetItemAt(_context.Menu.SelectedIndex).Text == "..")
            {
                return;
            }
            
            Console.CursorVisible = true;
            Console.Write($"{Color.Reset.ToAnsi()} Rename to: ");

            string? name = _context.ReadLine()?.Trim();
            if (name == null)
            {
                Console.CursorVisible = false;
                Console.Clear();
                _context.RedrawMenu();
                
                return;
            }
            
            char[] invalidNameChars = Path.GetInvalidFileNameChars();
            if (Encoding.Latin1.GetByteCount(name) != name.Length || name?.ToCharArray().Where(c => invalidNameChars.Contains(c)).ToList().Count > 0 || _context.Menu.GetItemsClone().Select(item => item.Text).Contains(name) || name == "..")
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
                    Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Are you sure? [Y/n]: ");
                    input = _context.ReadLine(escapeNo: true)?.Trim().ToLower();
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
                MenuItem? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (Directory.Exists(item?.Text))
                {
                    Directory.Move(item.Text, name);
                    
                    List<string> dirHistoryList = _context.DirHistory.ToList();
                    _context.DirHistory.Clear();
                    
                    for (int i = dirHistoryList.Count - 1; i >= 0; i--)
                    {
                        string dirPath = dirHistoryList[i];
                        if (dirPath == Path.GetFullPath(item.Text))
                        {
                            dirPath = Path.GetFullPath(name);
                        }
                        
                        _context.DirHistory.Push(dirPath);
                    }
                }
                else if (File.Exists(item?.Text))
                {
                    File.Move(item.Text, name);
                }
                
                item.Text = name;
            }
            catch { }
            
            Console.CursorVisible = false;
            Console.Clear();
        }
        
        _context.RedrawMenu();
    }
}