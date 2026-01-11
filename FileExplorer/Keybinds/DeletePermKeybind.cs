using CmdMenu;

namespace FileExplorer.Keybinds;

public class DeletePermKeybind(MenuContext context) : Keybind(context)
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
                string? input;

                lock (_context.OutLock)
                {
                    do
                    {
                        Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Are you sure you want to permanently delete {_context.SelectedItems.Count} items? [Y/n]: ");
                        input = _context.ReadLine()?.Trim();
                    
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                        
                            return;
                        }
                    } while (input != null && input != "y");   
                }
            }
            else
            {
                MenuItem item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (_context.SelectedItems.Count == 1)
                {
                    item = _context.SelectedItems[0];
                }

                if (item.Text == "..")
                {
                    Console.CursorVisible = false;
                    return;
                }

                items.Add(item);
                string? input;

                lock (_context.OutLock)
                {
                    do
                    {
                        Console.Write($"\x1b[2K\r{Color.Reset.ToAnsi()} Are you sure you want to permanently delete '{item.Text}'? [Y/n]: ");
                        input = _context.ReadLine()?.Trim();
                    
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                        
                            return;
                        }
                    } while (input != null && input != "y");
                }
            }

            foreach (string item in items.Select(item => item.Text))
            {
                try
                {
                    if (Directory.Exists(item))
                    {
                        Directory.Delete(item, true);
                        
                        List<string> tempHistory = new(_context.DirHistory);
                        tempHistory = tempHistory.Where(path => !path.Contains(Path.GetFullPath(item))).ToList();

                        _context.DirHistory = new(tempHistory);
                    }
                    else if (File.Exists(item))
                    {
                        File.Delete(item);
                    }
                }
                catch { }
            }
            
            Console.CursorVisible = false;
            Console.Clear();
            
            Task.Run(() =>
            {
                _context.RefreshItems();
            });
        }
    }
}