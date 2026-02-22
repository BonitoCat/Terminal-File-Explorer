using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;
using InputLib.EventArgs;
using LoggerLib;

namespace FileExplorer.Keybinds;

public class DuplicateKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            List<string> dupeItems = new();
            if (_context.SelectedItems.Count > 0)
            {
                dupeItems.AddRange(_context.SelectedItems.Select(item => Path.GetFullPath(item.Item.Text)));
            }
            else
            {
                CmdListBoxItem<CmdLabel>? item = _context.Menu.GetItemAt(_context.Menu.SelectedIndex);
                if (item?.Item.Text == "..")
                {
                    return;
                }
                    
                dupeItems.Add(Path.GetFullPath(item.Item.Text));
            }

            foreach (string itemPath in dupeItems)
            {
                int counter = 1;
                if (Directory.Exists(itemPath))
                {
                    string dirName = Path.GetFileName(itemPath);
                    string newName;

                    do
                    {
                        newName = $"{dirName}({counter++})";
                    } while (Directory.Exists(newName));
                        
                    _context.CopyDirectory(itemPath, newName);
                }
                else if (File.Exists(itemPath))
                {
                    string fileName = Path.GetFileNameWithoutExtension(itemPath);
                    string fileExt = Path.GetExtension(itemPath);
                    string newName;
                        
                    do
                    {
                        newName = $"{fileName}({counter++}){fileExt}";
                    } while (File.Exists(newName));
                        
                    File.Copy(itemPath, newName);
                }
            }
            
            Logger.LogI($"Duplicated {dupeItems.Count} items");
            
            _context.RefreshItems();
        }
    }
}