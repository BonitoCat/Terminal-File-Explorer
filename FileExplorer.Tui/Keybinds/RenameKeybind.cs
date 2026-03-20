using System.Text;
using TuiLib;
using TuiLib.Controls;
using FileExplorer.Tui.Context;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class RenameKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        Logger.LogI("Rename requested");
        
        lock (_context.Menu.Lock)
        {
            TuiListBoxItem<TuiLabel>? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
            if (item == null || item.Item.Text == "..")
            {
                return;
            }
            
            Logger.LogI("Reading rename input...");
            
            Console.CursorVisible = true;
            string? name = _context.Input($"{Color.Reset.ToAnsi()} Rename to: ", Color.TrimAnsi(item.Item.Text))?.Trim();
            
            if (name == null || name == Color.TrimAnsi(item.Item.Text))
            {
                Console.CursorVisible = false;
                Console.Clear();
                _context.RedrawMenu();
                
                Logger.LogI("Rename canceled");
                
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
                    
                    Logger.LogI("Rename canceled");
                
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
                    
                    Logger.LogI("Directory renamed");
                }
                else if (File.Exists(item?.Item.Text))
                {
                    File.Move(item.Item.Text, name);
                    Logger.LogI("File renamed");
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