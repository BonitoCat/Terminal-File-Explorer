using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class DeletePermKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        lock (_context.Menu.Lock)
        {
            List<CmdLabel> items = [];
            Console.CursorVisible = true;
            if (_context.SelectedItems.Count > 1)
            {
                items.AddRange(_context.SelectedItems.Select(item => item.Item));
                lock (_context.OutLock)
                {
                    string? input;
                    do
                    {
                        input = _context.Input(
                                $"\x1b[2K{Color.Reset.ToAnsi()} Are you sure you want to permanently delete {_context.SelectedItems.Count} items? [Y/n]: ",
                                enterNull: true,
                                escapeNo: true
                            )?.Trim().ToLower();
                    
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
                CmdListBoxItem<CmdLabel>? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (_context.SelectedItems.Count == 1)
                {
                    item = _context.SelectedItems[0];
                }

                if (item?.Item.Text == "..")
                {
                    Console.CursorVisible = false;
                    return;
                }

                items.Add(item.Item);
                lock (_context.OutLock)
                {
                    string? input;
                    do
                    {
                        input = _context.Input(
                                $"\x1b[2K{Color.Reset.ToAnsi()} Are you sure you want to permanently delete '{item.Item.Text}'? [Y/n]: ",
                                enterNull: true,
                                escapeNo: true
                            )?.Trim().ToLower();
                    
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
            
            _context.RefreshItems();
        }
    }
}