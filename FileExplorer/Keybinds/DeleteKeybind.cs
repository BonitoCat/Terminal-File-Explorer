using System.Diagnostics;
using CmdMenu;

namespace FileExplorer.Keybinds;

public class DeleteKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            List<MenuItem> items = [];
            Console.CursorVisible = true;
            if (_context.SelectedItems.Count > 1)
            {
                items.AddRange(_context.SelectedItems);
                lock (_context.OutLock)
                {
                    string? input;
                    do
                    {
                        Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Are you sure you want to move {_context.SelectedItems.Count} items to the recycle bin? [Y/n]: ");
                        input = _context.ReadLine(escapeNo: true)?.Trim().ToLower();
                    
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            _context.RedrawMenu();
                        
                            return;
                        }
                    } while (input != null && input != "y");
                }
            }
            else
            {
                MenuItem? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (_context.SelectedItems.Count == 1)
                {
                    item = _context.SelectedItems[0];
                }

                if (item?.Text == "..")
                {
                    Console.CursorVisible = false;
                    return;
                }

                items.Add(item);
                lock (_context.OutLock)
                {
                    string? input;
                    do
                    {
                        Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Are you sure you want to move '{item.Text}' to the recycle bin? [Y/n]: ");
                        input = _context.ReadLine(escapeNo: true)?.Trim().ToLower();
                        
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            _context.RedrawMenu();
                        
                            return;
                        }
                    } while (input != null && input != "y");
                }
            }

            try
            {
                foreach (string item in items.Select(item => item.Text))
                {
                    if (Directory.Exists(item))
                    {
                        List<string> tempHistory = new(_context.DirHistory);
                        tempHistory = tempHistory.Where(path => !path.Contains(Path.GetFullPath(item))).ToList();

                        _context.DirHistory = new(tempHistory);
                    }

                    ProcessStartInfo startInfo = new()
                    {
                        FileName = "gio",
                        Arguments = $"trash \"{item}\"",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    Process proc = new();
                    proc.StartInfo = startInfo;
                    
                    proc.Start();
                    proc.WaitForExit();
                }
            }
            catch { }
            
            Console.CursorVisible = false;
            Console.Clear();
            
            _context.RefreshItems();
        }
    }
}