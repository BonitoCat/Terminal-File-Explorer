using TuiLib.Controls;
using FileExplorer.Context;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class PasteKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        lock (_context.Menu.Lock)
        {
            Clipboard.ReadPaths(out ClipboardMode mode, out string[] paths);
            if (mode == ClipboardMode.Copy)
            {
                foreach (string itemPath in paths)
                {
                    try
                    {
                        if (Directory.Exists(itemPath))
                        {
                            _context.CopyDirectory(itemPath, Path.GetFileName(itemPath));
                        }
                        else if (File.Exists(itemPath))
                        {
                            File.Copy(itemPath, Path.GetFileName(itemPath));
                        }
                    }
                    catch { }
                }
            }
            else if (mode == ClipboardMode.Cut)
            {
                foreach (string itemPath in paths)
                {
                    try
                    {
                        string name = Path.GetFileName(itemPath);
                        if (Directory.Exists(itemPath))
                        {
                            Directory.Move(itemPath, name);
                            
                        }
                        else if (File.Exists(itemPath))
                        {
                            File.Move(itemPath, name);
                        }
                        else
                        {
                            continue;
                        }
                        
                        CmdListBoxItem<CmdLabel>? item = _context.Menu.Items.FirstOrDefault(item => item.Item.Text == name);
                        if (item != null)
                        {
                            _context.SelectedItems.Add(item);
                        }
                    }
                    catch { }
                }
            }
            else
            {
                return;
            }
        }
        
        _context.ClipboardContext.Items.Clear();
        _context.ClipboardContext.Mode = ClipboardMode.None;
        
        Clipboard.ClearPaths();
        
        Console.Clear();
        _context.RefreshItems();
    }
}