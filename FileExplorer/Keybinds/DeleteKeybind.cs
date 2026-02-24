using System.Diagnostics;
using TuiLib;
using TuiLib.Controls;
using FileExplorer.Context;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class DeleteKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        Logger.LogI("Move to recycle bin requested");
        
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
                                $"\x1b[2K{Color.Reset.ToAnsi()} Are you sure you want to move {_context.SelectedItems.Count} items to the recycle bin? [Y/n]: ",
                                enterNull: true,
                                escapeNo: true
                            )?.Trim().ToLower();
                    
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            _context.RedrawMenu();
                            
                            Logger.LogI($"Canceled moving {_context.SelectedItems.Count} items to recycle bin");
                        
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
                                $"\x1b[2K{Color.Reset.ToAnsi()} Are you sure you want to move '{item.Item.Text}' to the recycle bin? [Y/n]: ",
                                enterNull: true,
                                escapeNo: true
                            )?.Trim().ToLower();
                        
                        if (input == "n")
                        {
                            Console.CursorVisible = false;
                            Console.Clear();
                            _context.RedrawMenu();
                            
                            Logger.LogI("Canceled moving item to recycle bin");
                        
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
                
                Logger.LogI($"Moved {_context.SelectedItems.Count} items to the recycle bin");
            }
            catch { }
            
            Console.CursorVisible = false;
            Console.Clear();
            
            _context.RefreshItems();
        }
    }
}